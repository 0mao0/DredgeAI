import asyncio
import uuid

import httpx
from fastapi import APIRouter, File, UploadFile

from app.settings import settings

router = APIRouter()
_jobs: dict[str, dict] = {}
_sem = asyncio.Semaphore(settings.transcribe_max_concurrency)


@router.post("/transcribe")
async def start_transcribe(audio: UploadFile = File(...)):
    job_id = uuid.uuid4().hex
    data = await audio.read()
    _jobs[job_id] = {"status": "pending", "text": None}

    async def run():
        _jobs[job_id]["status"] = "running"
        try:
            async with _sem:
                async with httpx.AsyncClient(timeout=httpx.Timeout(600.0, connect=10.0)) as client:
                    resp = await client.post(
                        f"{settings.sensevoice_url}/asr",
                        headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                        files={"audio": ("audio.wav", data, "audio/wav")},
                    )
            if resp.status_code >= 400:
                _jobs[job_id] = {"status": "error", "text": f"转写失败: HTTP {resp.status_code}"}
                return
            _jobs[job_id] = {"status": "done", "text": resp.json()["text"]}
        except Exception as exc:
            _jobs[job_id] = {"status": "error", "text": f"转写失败: {exc}"}

    asyncio.create_task(run())
    return {"job_id": job_id}


@router.get("/transcribe/{job_id}")
def get_transcribe(job_id: str):
    job = _jobs.get(job_id)
    if not job:
        return {"status": "not_found"}
    return job
