from fastapi import APIRouter, UploadFile, File
from pydantic import BaseModel
from app.settings import settings
from app.engines.face import get_face_engine

router = APIRouter()
_engine = get_face_engine(settings.face_engine)


@router.post("/recognize")
async def recognize(image: UploadFile = File(...)):
    data = await image.read()
    faces = _engine.recognize(data)
    return {"faces": [f.__dict__ for f in faces]}


class EnrollRequest(BaseModel):
    worker_id: str


@router.post("/enroll")
async def enroll(worker_id: str = ..., image: UploadFile = File(...)):
    _engine.enroll(worker_id, await image.read())
    return {"ok": True}
