import httpx
from fastapi import APIRouter, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel

from app.settings import settings

router = APIRouter()


class TtsRequest(BaseModel):
    text: str


@router.post("/tts")
async def tts(req: TtsRequest):
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(300.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.cosyvoice_url}/api/tts",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                json={"text": req.text, "voice_id": settings.tts_voice_id, "speed": settings.tts_speed},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"TTS 服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"TTS 服务错误: HTTP {resp.status_code}")
    return StreamingResponse(iter([resp.content]), media_type=resp.headers.get("content-type", "audio/wav"))
