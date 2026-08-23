from fastapi import HTTPException, Request

from app.settings import settings


async def require_key(request: Request):
    if request.headers.get("X-Meeting-Bot-Key") != settings.meeting_bot_key:
        raise HTTPException(status_code=401, detail="invalid key")
