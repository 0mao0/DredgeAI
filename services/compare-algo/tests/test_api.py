from fastapi.testclient import TestClient

from app.main import BodySizeLimitMiddleware, app

client = TestClient(app)


def test_healthz():
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_similarity_endpoint(ir_payload):
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 1
    e = evidences[0]
    # Evidence 字段逐字遵守 spec §6.1
    assert set(e) == {
        "id", "taskId", "type", "severity", "docIds",
        "locations", "metrics", "title", "description", "aiGenerated",
    }
    assert e["type"] == "similarity"
    assert e["taskId"] == "task-001"
    assert e["aiGenerated"] is False


def test_pricing_endpoint(ir_payload):
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 1
    assert evidences[0]["type"] == "pricing"
    assert evidences[0]["metrics"]["pattern"] == "arithmetic"


def test_metadata_endpoint(ir_payload):
    r = client.post("/analyze/metadata", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 4
    kinds = {e["metrics"].get("field") or e["metrics"].get("pattern") for e in evidences}
    assert kinds == {"author", "createdAt", "creatorTool", "shared-typo"}
    assert all(e["type"] == "metadata" for e in evidences)


def test_invalid_bbox_returns_422_with_field_path(ir_payload):
    # bbox 超出 0~1 归一化区间（疑似像素坐标）→ 422；块级校验在字段级 validator 中报出
    ir_payload["documents"][0]["blocks"][0]["bbox"] = [0, 0, 99999, 10]
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422
    body = r.json()
    assert body["code"] == "IR_VALIDATION_FAILED"
    assert any("documents" in d["path"] for d in body["details"])
    assert any("bbox" in d["message"] for d in body["details"])


def test_missing_docmeta_field_returns_422(ir_payload):
    del ir_payload["documents"][0]["meta"]["docMeta"]["author"]  # 可 null 不可省略
    r = client.post("/analyze/metadata", json=ir_payload)
    assert r.status_code == 422
    assert r.json()["code"] == "IR_VALIDATION_FAILED"


def test_unknown_source_value_returns_422(ir_payload):
    # 实测词表 text/ocr/table/formula/null；其他值（如 v2 文档措辞 "native"）拒收
    ir_payload["documents"][0]["blocks"][0]["source"] = "native"
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422


def test_single_document_rejected(ir_payload):
    ir_payload["documents"] = ir_payload["documents"][:1]
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 422


def test_too_many_documents_rejected(ir_payload):
    ir_payload["documents"] = ir_payload["documents"] + ir_payload["documents"]  # 6 份
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 422


def test_duplicate_doc_ids_rejected(ir_payload):
    ir_payload["documents"] = ir_payload["documents"][:2]
    ir_payload["documents"][1] = dict(ir_payload["documents"][0])
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422


def test_empty_body_rejected():
    r = client.post("/analyze/similarity", json={})
    assert r.status_code == 422


def test_real_fixtures_end_to_end(raw_haigang_pair, raw_pingshen_pair):
    """真实产物端到端：海港对出 low 雷同证据；评审办法对出 2 条元数据证据。"""
    r = client.post("/analyze/similarity", json={
        "taskId": "task-real", "documents": raw_haigang_pair,
    })
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 1
    assert evidences[0]["severity"] == "low"
    assert evidences[0]["docIds"] == ["doc-12f45ca9", "doc-c8be9f8b"]

    r = client.post("/analyze/metadata", json={
        "taskId": "task-real", "documents": raw_pingshen_pair,
    })
    assert r.status_code == 200
    kinds = {e["metrics"].get("field") for e in r.json()["evidences"]}
    assert kinds == {"author", "creatorTool"}


def test_wrong_length_bbox_returns_422_with_clear_message(ir_payload):
    # 3 元素 bbox 不得塌陷为 pydantic "Field required"，须给出明确文案
    ir_payload["documents"][0]["blocks"][0]["bbox"] = [0.1, 0.2, 0.3]
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422
    body = r.json()
    assert body["code"] == "IR_VALIDATION_FAILED"
    assert any("bbox" in d["path"] for d in body["details"])
    assert any("4 元素" in d["message"] for d in body["details"])


def test_duplicate_block_uid_returns_422_with_document_path(ir_payload):
    # 文档级 model_validator 的错误无字段级 loc，须渲染为 documents[i] 且消息含 docId
    ir_payload["documents"][0]["blocks"].append(
        dict(ir_payload["documents"][0]["blocks"][0])
    )
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422
    body = r.json()
    assert body["code"] == "IR_VALIDATION_FAILED"
    assert any(d["path"].startswith("documents") for d in body["details"])
    assert any("doc-a" in d["message"] for d in body["details"])


def test_oversized_body_rejected_413(ir_payload):
    # parse-DoS 防御：超过限额的请求体直接 413，不进入解析
    limited = TestClient(BodySizeLimitMiddleware(app, max_body_bytes=1024))
    r = limited.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 413
    assert r.json()["code"] == "REQUEST_TOO_LARGE"
