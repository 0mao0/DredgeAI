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
import struct
import re
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
from fastapi.responses import Response, JSONResponse, StreamingResponse
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
        # FP16（autocast）在本地 4070 Laptop 上无实质提速且短句偶发空输出，
        # 默认保持 FP32；如需实验可设 COSYVOICE_FP16=1。
        fp16 = os.environ.get('COSYVOICE_FP16', '0') != '0'
        cosyvoice = AutoModel(model_dir=MODEL_DIR, fp16=fp16)
        _model_loaded = True
        print(f'[startup] Model loaded OK (fp16={fp16})', flush=True)
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


def _tensor_to_wav(ts):
    """单个音频 tensor -> 16bit PCM WAV 字节。"""
    tts = ts.squeeze().cpu().numpy()
    audio_i16 = (tts * 32767).clip(-32768, 32767).astype(np.int16)
    buf = io.BytesIO()
    with wave.open(buf, 'wb') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(cosyvoice.sample_rate)
        wf.writeframes(audio_i16.tobytes())
    return buf.getvalue(), len(audio_i16) / cosyvoice.sample_rate


def _iter_inference(text: str, instruct: str, voice_id: str = '', speed: float = 1.0):
    """逐句推理，按句 yield (wav_bytes, duration_sec)，供流式端点使用。"""
    if voice_id in VOICE_WAVS:
        prompt_wav = _ensure_wav(VOICE_WAVS[voice_id])
    else:
        prompt_wav = PROMPT_WAV_PATH
    gen = cosyvoice.inference_instruct2(text, instruct, prompt_wav, stream=False, speed=speed)
    for chunk in gen:
        yield _tensor_to_wav(chunk['tts_speech'])


def _split_sentences(text: str, max_chars: int = 30):
    """按断句切分：句末（。！？；\n）必断；长句再按逗号类标点切开；过短碎片并入下一段。"""
    parts = [p.strip() for p in re.split(r'(?<=[。！？；;\n])', text) if p.strip()]
    segments = []
    for part in parts:
        if len(part) <= max_chars:
            segments.append(part)
            continue
        clauses = [c.strip() for c in re.split(r'(?<=[，,、：:])', part) if c.strip()]
        buffer = ''
        for clause in clauses:
            if len(clause) > max_chars:
                if buffer:
                    segments.append(buffer)
                    buffer = ''
                for i in range(0, len(clause), max_chars):
                    segments.append(clause[i:i + max_chars])
                continue
            if buffer and len(buffer) + len(clause) > max_chars:
                segments.append(buffer)
                buffer = clause
            else:
                buffer += clause
        if buffer:
            segments.append(buffer)
    merged = []
    for segment in segments:
        if merged and len(merged[-1].replace(' ', '')) < 4:
            merged[-1] = merged[-1] + segment
        else:
            merged.append(segment)
    return merged


# 提示文本/提示音频/音色编码只跟音色有关，按音色缓存，避免每次请求重复走 CPU ONNX
_PREPROMPT_CACHE: dict = {}


def _prompt_parts(voice_id: str, prompt_wav: str, instruct: str):
    key = (voice_id or '', prompt_wav, instruct)
    if key not in _PREPROMPT_CACHE:
        prompt_text_token, prompt_text_token_len = cosyvoice.frontend._extract_text_token(instruct)
        speech_feat, speech_feat_len = cosyvoice.frontend._extract_speech_feat(prompt_wav)
        speech_token, speech_token_len = cosyvoice.frontend._extract_speech_token(prompt_wav)
        embedding = cosyvoice.frontend._extract_spk_embedding(prompt_wav)
        _PREPROMPT_CACHE[key] = (
            prompt_text_token, prompt_text_token_len,
            speech_feat, speech_feat_len,
            speech_token, speech_token_len,
            embedding,
        )
    return _PREPROMPT_CACHE[key]


def _iter_sentence_frames(text: str, instruct: str, prompt_wav: str, voice_id: str = '', speed: float = 1.0):
    """按断句逐句合成：提示编码跨句/跨请求复用，帧小、出得快。"""
    prompt_text_token, _prompt_text_token_len, speech_feat, _speech_feat_len, speech_token, _speech_token_len, embedding = \
        _prompt_parts(voice_id, prompt_wav, instruct)
    # 截断到匹配长度（用切片生成新张量，不修改缓存）
    token_len = min(int(speech_feat.shape[1] / 2), speech_token.shape[1])
    prompt_feat = speech_feat[:, :2 * token_len]
    prompt_speech_token = speech_token[:, :token_len]
    for sentence in _split_sentences(text):
        tts_text_token, tts_text_token_len = cosyvoice.frontend._extract_text_token(sentence)
        model_input = {
            'prompt_text': prompt_text_token,
            'prompt_text_len': torch.tensor([prompt_text_token.shape[1]], dtype=torch.int32),
            'flow_prompt_speech_token': prompt_speech_token,
            'flow_prompt_speech_token_len': torch.tensor([prompt_speech_token.shape[1]], dtype=torch.int32),
            'prompt_speech_feat': prompt_feat,
            'prompt_speech_feat_len': torch.tensor([prompt_feat.shape[1]], dtype=torch.int32),
            'flow_embedding': embedding,
            'llm_embedding': embedding,
            'text': tts_text_token,
            'text_len': tts_text_token_len,
        }
        try:
            for out in cosyvoice.model.tts(**model_input, stream=False, speed=speed):
                yield _tensor_to_wav(out['tts_speech'])
        except Exception as exc:
            # 单句失败跳过，不中断整段流式
            print(f'[stream] 句子合成失败，跳过: {sentence[:16]}... {exc}', flush=True)


def _run_inference(text: str, instruct: str, voice_id: str = '', speed: float = 1.0):
    """Runs on the calling thread. Returns (wav_bytes, duration_sec)."""
    all_audio = []
    total_duration = 0.0
    print(f'[debug] _run_inference calling inference_instruct2 text_len={len(text)} prompt=voice:{voice_id}', flush=True)
    for wav_bytes, duration in _iter_inference(text, instruct, voice_id, speed):
        all_audio.append(wav_bytes)
        total_duration += duration
    if not all_audio:
        raise RuntimeError('TTS produced no audio (empty result)')
    # 每句是独立 WAV，需先剥离 44 字节头、拼接 PCM 后重写一个 WAV
    pcm = b''.join(b[44:] for b in all_audio if len(b) > 44)
    if not pcm:
        raise RuntimeError('TTS produced no audio (empty result)')
    buf = io.BytesIO()
    with wave.open(buf, 'wb') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(cosyvoice.sample_rate)
        wf.writeframes(pcm)
    return buf.getvalue(), total_duration


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


@app.post('/api/tts/stream')
def generate_tts_stream(req: TTSRequest):
    """流式 TTS：整段文本一次请求，按句 yield 音频帧。

    帧格式：4 字节大端长度 + WAV 字节；length=0 表示结束。
    同步 def 让 FastAPI 在线程池中跑推理，避免阻塞事件循环。
    """
    if not _model_loaded:
        if _model_error:
            raise HTTPException(status_code=503, detail=f'Model load failed: {_model_error}')
        raise HTTPException(status_code=503, detail='Model not loaded yet')

    valid_ids = set(VOICE_PROMPTS.keys())
    voice_id = _resolve_voice_id(valid_ids, req.voice_id)
    instruct = VOICE_PROMPTS.get(voice_id, '')
    if voice_id in VOICE_WAVS:
        prompt_wav = _ensure_wav(VOICE_WAVS[voice_id])
    else:
        prompt_wav = PROMPT_WAV_PATH

    def frame_stream():
        try:
            for wav_bytes, _duration in _iter_sentence_frames(req.text, instruct, prompt_wav, voice_id, req.speed):
                yield struct.pack('>I', len(wav_bytes)) + wav_bytes
            yield struct.pack('>I', 0)
        except Exception as e:
            import traceback
            traceback.print_exc()
            raise

    return StreamingResponse(frame_stream(), media_type='application/octet-stream')


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
