from fastapi import Depends, FastAPI

from app.security import require_key

app = FastAPI(title="meeting-bot", dependencies=[Depends(require_key)])


@app.get("/health")
def health():
    return {"status": "ok"}
