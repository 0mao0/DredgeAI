from fastapi import Depends, FastAPI

from app.security import require_key
from app.settings import settings
from app.routes.count import router as count_router

app = FastAPI(title="yolo", dependencies=[Depends(require_key)])
app.include_router(count_router)


@app.get("/health")
def health():
    return {"status": "ok"}


@app.on_event("startup")
def startup():
    from app.engines.count import get_count_engine

    app.state.count_engine = get_count_engine(settings.count_engine)
