from fastapi import Depends, FastAPI

from app.security import require_key
from app.settings import settings
from app.routes.face import router as face_router

app = FastAPI(title="insightface", dependencies=[Depends(require_key)])
app.include_router(face_router)


@app.get("/health")
def health():
    return {"status": "ok"}


@app.on_event("startup")
def startup():
    from app.engines.face import get_face_engine

    app.state.face_engine = get_face_engine(settings.face_engine)
