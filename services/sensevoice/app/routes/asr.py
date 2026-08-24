from fastapi import APIRouter, File, Request, UploadFile

router = APIRouter()


@router.post("/asr")
def asr(request: Request, audio: UploadFile = File(...)):
    engine = request.app.state.asr_engine
    data = audio.file.read()
    result = engine.transcribe(data)
    return {"text": result.text}
