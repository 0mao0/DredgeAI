from fastapi import APIRouter, File, UploadFile

from app.engines.asr import get_asr_engine
from app.settings import settings

router = APIRouter()
_engine = get_asr_engine(settings.asr_engine)


@router.post("/asr")
async def asr(audio: UploadFile = File(...)):
    data = await audio.read()
    result = _engine.transcribe(data)
    return {"text": result.text}
