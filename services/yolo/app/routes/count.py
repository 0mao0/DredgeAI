from fastapi import APIRouter, File, Request, UploadFile

router = APIRouter()


@router.post("/count")
async def count(request: Request, image: UploadFile = File(...)):
    n = request.app.state.count_engine.count(await image.read())
    return {"count": n}
