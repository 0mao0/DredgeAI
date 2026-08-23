from fastapi import APIRouter, UploadFile, File
from app.settings import settings
from app.engines.count import get_count_engine

router = APIRouter()
_engine = get_count_engine(settings.count_engine)


@router.post("/count")
async def count(image: UploadFile = File(...)):
    n = _engine.count(await image.read())
    return {"count": n}
