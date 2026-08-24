import httpx
from fastapi import APIRouter, File, HTTPException, Request, UploadFile

from app.settings import settings

router = APIRouter()


@router.post("/asr")
async def asr(request: Request, audio: UploadFile = File(...)):
    data = await audio.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(300.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.sensevoice_url}/asr",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                files={"audio": (audio.filename or "audio.wav", data, audio.content_type or "audio/wav")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"ASR 服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"ASR 服务错误: HTTP {resp.status_code}")
    return resp.json()
