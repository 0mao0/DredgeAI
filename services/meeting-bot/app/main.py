from fastapi import FastAPI

from app.settings import settings

app = FastAPI(title="meeting-bot")


@app.get("/health")
def health():
    return {"status": "ok"}
