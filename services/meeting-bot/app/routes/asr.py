from fastapi import APIRouter, UploadFile, File
from app.settings import settings
from app.engines.asr import get_asr_engine

router = APIRouter()
_engine = get_asr_engine(settings.asr_engine)


@router.post("/asr")
async def asr(audio: UploadFile = File(...)):
    data = await audio.read()
    result = _engine.transcribe(data)
    return {"text": result.text}
