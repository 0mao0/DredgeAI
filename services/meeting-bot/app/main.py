from fastapi import Depends, FastAPI

from app.routes.asr import router as asr_router
from app.security import require_key

app = FastAPI(title="meeting-bot", dependencies=[Depends(require_key)])
app.include_router(asr_router)


@app.get("/health")
def health():
    return {"status": "ok"}
