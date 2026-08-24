from fastapi import APIRouter, File, Form, Request, UploadFile

router = APIRouter()


@router.post("/recognize")
async def recognize(request: Request, image: UploadFile = File(...)):
    data = await image.read()
    faces = request.app.state.face_engine.recognize(data)
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
    request: Request,
    worker_id: str = Form(...),
    name: str = Form(""),
    image: UploadFile = File(...),
):
    request.app.state.face_engine.enroll(worker_id, await image.read(), name=name)
    return {"ok": True}
