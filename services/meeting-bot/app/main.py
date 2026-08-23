from fastapi import Depends, FastAPI

from app.security import require_key
from app.routes.asr import router as asr_router
from app.routes.tts import router as tts_router
from app.routes.face import router as face_router
from app.routes.count import router as count_router
from app.routes.transcribe import router as transcribe_router

app = FastAPI(title="meeting-bot", dependencies=[Depends(require_key)])
app.include_router(asr_router)
app.include_router(tts_router)
app.include_router(face_router)
app.include_router(count_router)
app.include_router(transcribe_router)


@app.get("/health")
def health():
    return {"status": "ok"}
