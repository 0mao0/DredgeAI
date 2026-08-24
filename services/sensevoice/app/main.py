from fastapi import Depends, FastAPI

from app.security import require_key
from app.settings import settings
from app.routes.asr import router as asr_router

app = FastAPI(title="sensevoice", dependencies=[Depends(require_key)])
app.include_router(asr_router)


@app.get("/health")
def health():
    return {"status": "ok", "model_loaded": app.state.asr_engine.loaded}


@app.on_event("startup")
def startup():
    from app.engines.sensevoice_asr import SenseVoiceAsrEngine

    app.state.asr_engine = SenseVoiceAsrEngine(
        model_dir=settings.model_dir,
        device=settings.asr_device,
    )
