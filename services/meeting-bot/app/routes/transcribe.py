import asyncio
import uuid
from fastapi import APIRouter, UploadFile, File
from app.settings import settings
from app.engines.asr import get_asr_engine

router = APIRouter()
_engine = get_asr_engine("firered" if settings.asr_engine == "firered" else "mock")
_jobs: dict[str, dict] = {}


@router.post("/transcribe")
async def start_transcribe(audio: UploadFile = File(...)):
    job_id = uuid.uuid4().hex
    data = await audio.read()
    _jobs[job_id] = {"status": "pending", "text": None}

    async def run():
        _jobs[job_id]["status"] = "running"
        _jobs[job_id]["text"] = _engine.transcribe(data).text
        _jobs[job_id]["status"] = "done"

    asyncio.create_task(run())
    return {"job_id": job_id}


@router.get("/transcribe/{job_id}")
def get_transcribe(job_id: str):
    job = _jobs.get(job_id)
    if not job:
        return {"status": "not_found"}
    return job
