from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from app.settings import settings
from app.engines.tts import get_tts_engine

router = APIRouter()
_engine = get_tts_engine(settings.tts_engine)


class TtsRequest(BaseModel):
    text: str


@router.post("/tts")
def tts(req: TtsRequest):
    audio = _engine.synthesize(req.text)
    return StreamingResponse(iter([audio]), media_type="audio/wav")
