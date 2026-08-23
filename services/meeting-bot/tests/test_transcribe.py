from fastapi.testclient import TestClient
from app.main import app


def test_transcribe_job_flow():
    client = TestClient(app)
    resp = client.post("/transcribe", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"audio": ("m.wav", b"RIFF-fake-wav", "audio/wav")})
    assert resp.status_code == 200
    job_id = resp.json()["job_id"]
    status = client.get(f"/transcribe/{job_id}", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert status.status_code == 200
    assert "status" in status.json()
