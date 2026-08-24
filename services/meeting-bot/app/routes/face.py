import httpx
from fastapi import APIRouter, File, Form, HTTPException, Request, UploadFile

from app.settings import settings

router = APIRouter()


@router.post("/recognize")
async def recognize(request: Request, image: UploadFile = File(...)):
    data = await image.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(120.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.insightface_url}/recognize",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                files={"image": (image.filename or "image.jpg", data, image.content_type or "image/jpeg")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"人脸服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"人脸服务错误: HTTP {resp.status_code}")
    return resp.json()


@router.post("/enroll")
async def enroll(
    request: Request,
    worker_id: str = Form(...),
    name: str = Form(""),
    image: UploadFile = File(...),
):
    data = await image.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(120.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.insightface_url}/enroll",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                data={"worker_id": worker_id, "name": name},
                files={"image": (image.filename or "image.jpg", data, image.content_type or "image/jpeg")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"人脸服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"人脸服务错误: HTTP {resp.status_code}")
    return resp.json()
