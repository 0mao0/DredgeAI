"""
CosyVoice 3 FastAPI TTS service — shared, multi-project.
Usage:  python server.py --port 8000
Endpoints:
  GET  /api/voices      voice list
  POST /api/tts         text -> speech (WAV)
  GET  /api/health      health + GPU status
"""
import os
import sys
import io
import wave
import logging

logging.getLogger('matplotlib').setLevel(logging.WARNING)
logging.getLogger('httpx').setLevel(logging.WARNING)
logging.getLogger('httpcore').setLevel(logging.WARNING)

ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.environ.get('COSYVOICE_DATA', ROOT_DIR)
SRC_DIR = os.environ.get('COSYVOICE_SRC', os.path.join(ROOT_DIR, 'CosyVoice'))
COSYVOICE_DIR = os.path.join(DATA_DIR, 'CosyVoice')
ASSET_DIR = os.path.join(COSYVOICE_DIR, 'asset')
SAMPLES_DIR = os.path.join(ASSET_DIR, 'samples')
CONFIG_PATH = os.environ.get('VOICES_CONFIG', os.path.join(DATA_DIR, 'voices_config.json'))
SAMPLE_TEXT = '你好，欢迎试听我的声音，希望你喜欢。'
MODEL_DIR = os.environ.get('COSYVOICE_MODEL_DIR', os.path.join(DATA_DIR, 'pretrained_models', 'Fun-CosyVoice3-0.5B'))
PROMPT_WAV_PATH = os.path.join(ASSET_DIR, '男声-播报_converted_norm.wav')
DEFAULT_VOICE_ID = os.environ.get('TTS_VOICE_ID', 'zh-male-news')

sys.path.insert(0, SRC_DIR)
sys.path.insert(0, os.path.join(SRC_DIR, 'third_party', 'Matcha-TTS'))

import torch
from fastapi import Depends, FastAPI, HTTPException, Request, UploadFile, File, Form
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import Response, JSONResponse
from pydantic import BaseModel, Field
import uvicorn
import numpy as np
import json, uuid, shutil


# --- Voice config: loaded from voices_config.json ---
def _load_config():
    if os.path.exists(CONFIG_PATH):
        with open(CONFIG_PATH, encoding='utf-8') as f:
            return json.load(f)
    return {'version': '2.0', 'voices': {}}


def _save_config(cfg):
    with open(CONFIG_PATH, 'w', encoding='utf-8') as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)


_cfg = _load_config()

# Build runtime lookups from config
VOICE_PROMPTS = {}
VOICE_WAVS = {}
_voices = []
for vid, v in _cfg.get('voices', {}).items():
    VOICE_PROMPTS[vid] = v['instruct']
    VOICE_WAVS[vid] = os.path.join(DATA_DIR, v['wav']) if not os.path.isabs(v['wav']) else v['wav']
    _voices.append({
        'id': vid, 'name': v['name'], 'category': v.get('category', ''), 'gender': v.get('gender', ''),
        'style': v.get('style', ''), 'provider': 'CosyVoice 3', 'tags': v.get('tags', []),
        'visibility': v.get('visibility', 'public'),
    })


def _reload_voices():
    """Rebuild VOICE_PROMPTS, VOICE_WAVS, _voices from current config."""
    global VOICE_PROMPTS, VOICE_WAVS, _voices
    cfg = _load_config()
    VOICE_PROMPTS.clear()
    VOICE_WAVS.clear()
    _voices.clear()
    for vid, v in cfg.get('voices', {}).items():
        VOICE_PROMPTS[vid] = v['instruct']
        wav_path = os.path.join(DATA_DIR, v['wav']) if not os.path.isabs(v['wav']) else v['wav']
        VOICE_WAVS[vid] = wav_path
        _voices.append({
            'id': vid, 'name': v['name'], 'category': v.get('category', ''), 'gender': v.get('gender', ''),
            'style': v.get('style', ''), 'provider': 'CosyVoice 3', 'tags': v.get('tags', []),
        })


def _normalize_wav(in_path: str, out_path: str):
    """Apply EBU R128 loudness normalization."""
    import subprocess as _sp
    _sp.run(['ffmpeg', '-y', '-i', in_path,
             '-af', 'loudnorm=I=-23:LRA=7:TP=-1',
             '-ar', '24000', '-ac', '1', '-sample_fmt', 's16', out_path],
            capture_output=True, check=True)


MEETING_BOT_KEY = os.environ.get('MEETING_BOT_KEY', 'dev-key')


async def require_key(request: Request):
    if request.headers.get('X-Meeting-Bot-Key') != MEETING_BOT_KEY:
        raise HTTPException(status_code=401, detail='invalid key')


def _resolve_voice_id(valid_ids, voice_id: str) -> str:
    return voice_id if voice_id in valid_ids else DEFAULT_VOICE_ID


app = FastAPI(title='CosyVoice 3 TTS API', version='2.0.0',
              dependencies=[Depends(require_key)])
app.add_middleware(CORSMiddleware, allow_origins=['*'], allow_credentials=True,
                   allow_methods=['*'], allow_headers=['*'])

cosyvoice = None
_model_loaded = False
_model_error = None


class TTSRequest(BaseModel):
    text: str = Field(..., min_length=1, max_length=5000, description='要合成的文本')
    voice_id: str = Field(default='', description='音色ID')
    speed: float = Field(default=1.0, ge=0.5, le=3.0, description='语速(0.5-3.0)')


class VoiceItem(BaseModel):
    id: str
    name: str
    category: str
    gender: str
    style: str
    provider: str
    tags: list = []


# --- Model load + inference ----------------------------------------------
# CosyVoice3 must create its CUDA context on the main thread. We load the model
# synchronously during startup (which runs on the event-loop / main thread).
# Each /api/tts call runs inference in a single-worker executor so the model
# object is never touched concurrently.

def _load_model():
    global cosyvoice, _model_loaded, _model_error
    try:
        from cosyvoice.cli.cosyvoice import AutoModel
        print(f'[startup] Loading CosyVoice3 from {MODEL_DIR} ...', flush=True)
        cosyvoice = AutoModel(model_dir=MODEL_DIR, fp16=True)
        _model_loaded = True
        print('[startup] Model loaded OK', flush=True)
    except Exception as e:
        _model_error = repr(e)
        print('[startup] Model load FAILED:', e, flush=True)


def _pin_local_wetext():
    """Use the already-downloaded wetext files instead of re-checking ModelScope.

    CosyVoiceFrontEnd imports ``wetext`` during model load, and wetext calls
    ``modelscope.snapshot_download('pengzhendong/wetext')`` on every startup.
    ModelScope can return 403 when no token is configured, which would make the
    text frontend silently degrade to no normalization. The files are already
    present in the shared volume, so pin that path for this model id only.
    """
    try:
        import modelscope
        from pathlib import Path

        cache_root = Path(os.environ.get('MODELSCOPE_CACHE', '/data/modelscope'))
        local = cache_root / 'hub' / 'pengzhendong' / 'wetext'
        original = modelscope.snapshot_download

        def pinned_snapshot_download(model_id, *args, **kwargs):
            if model_id == 'pengzhendong/wetext' and local.exists():
                return str(local)
            return original(model_id, *args, **kwargs)

        modelscope.snapshot_download = pinned_snapshot_download
    except Exception as exc:
        print('[startup] pin local wetext failed:', exc, flush=True)


def _ensure_wav(path: str) -> str:
    """Convert m4a/mp3/ogg to wav on-the-fly if needed. Returns wav path."""
    ext = os.path.splitext(path)[1].lower()
    if ext not in ('.m4a', '.mp3', '.ogg', '.aac'):
        return path
    wav_path = os.path.splitext(path)[0] + '_converted.wav'
    if not os.path.exists(wav_path):
        import subprocess as _sp
        _sp.run(['ffmpeg', '-y', '-i', path, '-ar', '24000', '-ac', '1',
                 '-sample_fmt', 's16', wav_path],
                capture_output=True, check=True)
    return wav_path


def _run_inference(text: str, instruct: str, voice_id: str = '', speed: float = 1.0):
    """Runs on the calling thread. Returns (wav_bytes, duration_sec)."""
    if voice_id in VOICE_WAVS:
        prompt_wav = _ensure_wav(VOICE_WAVS[voice_id])
    else:
        prompt_wav = PROMPT_WAV_PATH
    all_audio = []
    import time as _t
    print(f'[debug] _run_inference calling inference_instruct2 text_len={len(text)} prompt={prompt_wav}', flush=True)
    gen = cosyvoice.inference_instruct2(text, instruct, prompt_wav, stream=False, speed=speed)
    chunk_count = 0
    for chunk in gen:
        chunk_count += 1
        ts = chunk['tts_speech']
        print(f'[debug]  chunk {chunk_count} shape={ts.shape} device={ts.device}', flush=True)
        tts = ts.squeeze().cpu().numpy()
        all_audio.append(tts)
    print(f'[debug] _run_inference done chunks={chunk_count}', flush=True)
    if not all_audio:
        raise RuntimeError('TTS produced no audio (empty result)')
    audio_data = np.concatenate(all_audio)
    audio_i16 = (audio_data * 32767).clip(-32768, 32767).astype(np.int16)
    buf = io.BytesIO()
    with wave.open(buf, 'wb') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(cosyvoice.sample_rate)
        wf.writeframes(audio_i16.tobytes())
    return buf.getvalue(), len(audio_i16) / cosyvoice.sample_rate


def _pregen_samples():
    """Pre-generate a short audio sample for each voice so clients can preview instantly."""
    os.makedirs(SAMPLES_DIR, exist_ok=True)
    for voice_id in _voices:
        vid = voice_id['id']
        sample_path = os.path.join(SAMPLES_DIR, f'{vid}.wav')
        if os.path.exists(sample_path):
            continue
        try:
            print(f'[samples] Generating sample for {vid} ...', flush=True)
            instruct = VOICE_PROMPTS.get(vid, '')
            wav_bytes, _ = _run_inference(SAMPLE_TEXT, instruct, vid)
            with open(sample_path, 'wb') as f:
                f.write(wav_bytes)
            print(f'[samples]  {vid} done ({len(wav_bytes)} bytes)', flush=True)
        except Exception as e:
            print(f'[samples]  {vid} FAILED: {e}', flush=True)


@app.on_event('startup')
async def startup():
    # Blocking load on the main thread so the CUDA context is created there.
    _pin_local_wetext()
    _load_model()
    _pregen_samples()


@app.get('/api/voices', response_model=list[VoiceItem])
async def get_voices():
    return _voices


@app.post('/api/tts')
async def generate_tts(req: TTSRequest):
    if not _model_loaded:
        if _model_error:
            raise HTTPException(status_code=503, detail=f'Model load failed: {_model_error}')
        raise HTTPException(status_code=503, detail='Model not loaded yet')

    valid_ids = set(VOICE_PROMPTS.keys())
    voice_id = _resolve_voice_id(valid_ids, req.voice_id)
    instruct = VOICE_PROMPTS.get(voice_id, '')

    try:
        # Run inference synchronously on the SAME thread that loaded the model
        # (uvicorn's event-loop / main thread). CosyVoice3 requires same-thread
        # CUDA context for the pipeline to produce audio.
        import time as _t
        _t0 = _t.time()
        import threading
        print(f'[debug] _run_inference start thread={threading.current_thread().name} voice={voice_id}', flush=True)
        wav_bytes, duration = _run_inference(req.text, instruct, voice_id, req.speed)
        print(f'[debug] _run_inference OK t={_t.time()-_t0:.2f}s size={len(wav_bytes)}', flush=True)
    except Exception as e:
        import traceback
        traceback.print_exc()
        print(f'[debug] _run_inference FAILED: {e}', flush=True)
        raise HTTPException(status_code=500, detail=str(e))

    return Response(content=wav_bytes, media_type='audio/wav',
                    headers={'X-Duration-Sec': f'{duration:.2f}'})


@app.get('/api/samples/{voice_id}.wav')
async def get_sample(voice_id: str):
    """Return a pre-generated audio sample for the given voice."""
    sample_path = os.path.join(SAMPLES_DIR, f'{voice_id}.wav')
    if os.path.exists(sample_path):
        return Response(content=open(sample_path, 'rb').read(), media_type='audio/wav')
    raise HTTPException(status_code=404, detail='Sample not found')


@app.post('/api/voices/upload')
async def upload_voice(file: UploadFile = File(...), name: str = Form(...)):
    """Upload a voice recording. Converts, normalizes, registers, and pre-generates a sample.
    
    Returns the new voice_id and sample URL.
    """
    vid = 'voice_' + uuid.uuid4().hex[:8]
    raw_path = os.path.join(ASSET_DIR, f'{vid}_raw.wav')
    norm_path = os.path.join(ASSET_DIR, f'{vid}_norm.wav')
    sample_path = os.path.join(SAMPLES_DIR, f'{vid}.wav')

    # Save uploaded file
    raw_bytes = await file.read()
    with open(raw_path, 'wb') as f:
        f.write(raw_bytes)

    # If not already wav, convert
    ext = os.path.splitext(file.filename or '')[1].lower()
    if ext not in ('.wav',):
        wav_tmp = raw_path + '_tmp.wav'
        import subprocess as _sp
        _sp.run(['ffmpeg', '-y', '-i', raw_path, '-ar', '24000', '-ac', '1',
                 '-sample_fmt', 's16', wav_tmp], capture_output=True, check=True)
        os.remove(raw_path)
        os.rename(wav_tmp, raw_path)

    # Normalize (loudnorm)
    _normalize_wav(raw_path, norm_path)
    os.remove(raw_path)

    config_wav_path = os.path.join('CosyVoice', 'asset', f'{vid}_norm.wav')
    instruct = f'You are a helpful assistant. 请用自然的语气朗读。<|endofprompt|>'

    # Register in config
    cfg = _load_config()
    cfg.setdefault('voices', {})[vid] = {
        'name': name, 'category': '用户上传', 'gender': '',
        'style': '', 'tags': ['用户', '自定义'], 'visibility': 'private',
        'wav': config_wav_path.replace('\\', '/'),
        'instruct': instruct,
    }
    _save_config(cfg)
    _reload_voices()

    # Pre-generate sample
    if cosyvoice and _model_loaded:
        try:
            wav_bytes, _ = _run_inference(SAMPLE_TEXT, instruct, vid)
            os.makedirs(SAMPLES_DIR, exist_ok=True)
            with open(sample_path, 'wb') as f:
                f.write(wav_bytes)
        except Exception as e:
            print(f'[upload] sample generation failed: {e}', flush=True)

    return {
        'voice_id': vid,
        'name': name,
        'sample_url': f'/api/samples/{vid}.wav',
        'message': f'Voice "{name}" registered successfully.',
    }


@app.get('/api/health')
async def health():
    gpu_ok = torch.cuda.is_available()
    gpu_name = torch.cuda.get_device_name(0) if gpu_ok else None
    return JSONResponse({
        'status': 'ok' if _model_loaded else 'loading',
        'model_loaded': _model_loaded,
        'gpu': gpu_ok,
        'gpu_name': gpu_name,
    })


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=8000)
    parser.add_argument('--host', type=str, default='0.0.0.0')
    args = parser.parse_args()
    uvicorn.run(app, host=args.host, port=args.port)
