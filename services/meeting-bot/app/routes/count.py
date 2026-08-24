import httpx
from fastapi import APIRouter, File, HTTPException, Request, UploadFile

from app.settings import settings

router = APIRouter()


@router.post("/count")
async def count(request: Request, image: UploadFile = File(...)):
    data = await image.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(120.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.yolo_url}/count",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                files={"image": (image.filename or "image.jpg", data, image.content_type or "image/jpeg")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"人数服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"人数服务错误: HTTP {resp.status_code}")
    return resp.json()
