import asyncio
import copy
import logging

from fastapi.testclient import TestClient

from app.main import BodySizeLimitMiddleware, _max_body_bytes, app

client = TestClient(app)


def test_healthz():
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_similarity_endpoint(ir_payload):
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    visible = [e for e in evidences if not e["metrics"].get("matrixOnly")]
    matrix_only = [e for e in evidences if e["metrics"].get("matrixOnly")]
    assert len(visible) == 1
    assert len(matrix_only) == 2
    e = visible[0]
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


def test_similarity_endpoint_ocr_downgrade_pinned(ir_payload):
    # 合成 fixture：doc-a/doc-b 雷同（Dice>0.8 本应 high）但命中 doc-b 低置信 OCR 块
    # → 端点级钉：severity 降 mid + ocrSuspect=true（service 层已覆盖，此处钉接口透出）
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    e = next(e for e in evidences if not e["metrics"].get("matrixOnly"))
    assert e["severity"] == "mid"
    assert e["metrics"]["ocrSuspect"] is True
    assert e["metrics"]["similarity"] > 0.8


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
    # 上限 8 份（对齐 C# 端 MaxBidDocuments=8）：9 份唯一 docId → 422
    docs = []
    for i in range(9):
        d = copy.deepcopy(ir_payload["documents"][i % 3])
        d["docId"] = f"doc-extra-{i}"
        docs.append(d)
    ir_payload["documents"] = docs
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 422


def test_eight_documents_accepted(ir_payload):
    # 8 份上限边界：唯一 docId 的 8 份产物不再 422（修复前 6~8 份必然 422 拖垮整个任务）
    docs = []
    for i in range(8):
        d = copy.deepcopy(ir_payload["documents"][i % 3])
        d["docId"] = f"doc-extra-{i}"
        docs.append(d)
    ir_payload["documents"] = docs
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 200


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


def test_adapter_level_validation_error_returns_422(ir_payload):
    # 幽灵 outline 锚点：产物层校验通过（raw 不查锚点存在性），适配为 IrDocument 时
    # _check_document 抛 pydantic ValidationError → 适配层 422（与请求体校验同码不同源）
    ir_payload["documents"][0]["meta"]["outlines"] = [{
        "outline_id": "o1", "title": "幽灵章节", "level": 1,
        "page_idx": 0, "anchor_block_id": "no-such-block",
    }]
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422
    body = r.json()
    assert body["code"] == "IR_VALIDATION_FAILED"
    assert any("blockId" in d["message"] for d in body["details"])


def test_oversized_body_rejected_413(ir_payload):
    # parse-DoS 防御：超过限额的请求体直接 413，不进入解析
    limited = TestClient(BodySizeLimitMiddleware(app, max_body_bytes=1024))
    r = limited.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 413
    assert r.json()["code"] == "REQUEST_TOO_LARGE"
    assert "content-length" in r.headers


def test_response_validation_failure_returns_500_not_422(monkeypatch, ir_payload):
    # 服务端产出违反响应契约（ResponseValidationError）是服务端 bug → 500，
    # 不得按调用方数据错误处理；handler 捕获后不再向上抛（TestClient 默认
    # raise_server_exceptions=True，若异常穿透中间件本用例会直接 error）
    monkeypatch.setattr(
        "app.main.AnalyzeResponse",
        lambda **kwargs: {"evidences": [{"unexpected": "garbage"}]},
    )
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 500
    assert r.json()["code"] == "INTERNAL_ERROR"


def test_max_body_bytes_defaults_without_env(monkeypatch):
    monkeypatch.delenv("COMPARE_ALGO_MAX_BODY_BYTES", raising=False)
    assert _max_body_bytes() == 96 * 1024 * 1024


def test_max_body_bytes_valid_env(monkeypatch):
    monkeypatch.setenv("COMPARE_ALGO_MAX_BODY_BYTES", "1048576")
    assert _max_body_bytes() == 1048576


def test_max_body_bytes_invalid_env_falls_back_with_warning(monkeypatch, caplog):
    # "96MB" 非纯数字 → 回退默认并告警（不得静默）
    monkeypatch.setenv("COMPARE_ALGO_MAX_BODY_BYTES", "96MB")
    with caplog.at_level(logging.WARNING, logger="compare-algo"):
        assert _max_body_bytes() == 96 * 1024 * 1024
    assert any("COMPARE_ALGO_MAX_BODY_BYTES" in r.message for r in caplog.records)
    caplog.clear()
    # "²".isdigit() 为 True 但无法解析为 int → 同样回退并告警
    monkeypatch.setenv("COMPARE_ALGO_MAX_BODY_BYTES", "²")
    with caplog.at_level(logging.WARNING, logger="compare-algo"):
        assert _max_body_bytes() == 96 * 1024 * 1024
    assert any("COMPARE_ALGO_MAX_BODY_BYTES" in r.message for r in caplog.records)


def test_middleware_client_disconnect_returns_early():
    # 客户端中途断开：不得把截断的 body 回放给下游
    downstream_called = False

    async def downstream(scope, receive, send):
        nonlocal downstream_called
        downstream_called = True

    mw = BodySizeLimitMiddleware(downstream, max_body_bytes=1024)
    messages = iter([
        {"type": "http.request", "body": b"partial", "more_body": True},
        {"type": "http.disconnect"},
    ])

    async def receive():
        return next(messages)

    async def send(message):
        pass

    asyncio.run(mw({"type": "http", "headers": []}, receive, send))
    assert not downstream_called


def test_oversized_declared_content_length_fast_path():
    # 快路径：声明的 Content-Length 已超限 → 直接 413（带 Content-Length 头），
    # 不读取 body；字节计数仍是对 chunk/谎报场景的真正防线
    async def downstream(scope, receive, send):
        raise AssertionError("声明超限时不应进入下游")

    mw = BodySizeLimitMiddleware(downstream, max_body_bytes=1024)
    sent = []

    async def receive():
        raise AssertionError("声明超限时不应读取 body")

    async def send(message):
        sent.append(message)

    scope = {"type": "http", "headers": [(b"content-length", b"2048")]}
    asyncio.run(mw(scope, receive, send))
    assert sent[0]["type"] == "http.response.start"
    assert sent[0]["status"] == 413
    header_names = [k for k, _ in sent[0]["headers"]]
    assert b"content-length" in header_names
