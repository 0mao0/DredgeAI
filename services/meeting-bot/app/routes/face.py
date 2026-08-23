from fastapi import APIRouter, UploadFile, File, Form
from app.settings import settings
from app.engines.face import get_face_engine

router = APIRouter()
_engine = get_face_engine(settings.face_engine)


@router.post("/recognize")
async def recognize(image: UploadFile = File(...)):
    data = await image.read()
    faces = _engine.recognize(data)
    return {"faces": [
        {
            "workerId": f.worker_id,
            "name": f.name,
            "confidence": f.confidence,
            "bbox": f.bbox,
        }
        for f in faces
    ]}
@router.post("/enroll")
async def enroll(
    worker_id: str = Form(...),
    name: str = Form(""),
    image: UploadFile = File(...),
):
    _engine.enroll(worker_id, await image.read(), name=name)
    return {"ok": True}
