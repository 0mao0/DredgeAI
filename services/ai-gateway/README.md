# ai-gateway DredgeAI AI 推理网关

**服务定位**：平台唯一 AI 推理网关（涉及 LLM 的必选基础套件）。ABP 的 `ILlmGateway` 与前端对话均经此转发，不直连模型。

消费 `angineer-ai-inference@v0.1.0`，对外提供 OpenAI 兼容 chat 与 SSE 流式；多模型路由、重试、熔断由库负责。
网关不持久化用量，经 `AI_GATEWAY_USAGE_REPORT_URL` 回传 ABP。

## 启动

```bash
uv sync
uv run uvicorn app.main:app --host 0.0.0.0 --port 8200
```

## 测试

```bash
uv run pytest -q
```

## 接口

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | /healthz | 健康检查 |
| GET | /v1/models | 模型配置（api_key 脱敏） |
| POST | /v1/chat | 非流式对话 |
| POST | /v1/chat/stream | SSE 流式对话 |

## 环境变量

LLM 配置见 `angineer-ai-inference` 文档（`LLM_CONFIGS` + `ANGINEER_*`）；
本服务自身配置前缀 `AI_GATEWAY_`：`AI_GATEWAY_API_TOKEN`（入站校验，空=关闭）、`AI_GATEWAY_USAGE_REPORT_URL`、`AI_GATEWAY_USAGE_REPORT_ENABLED`、`AI_GATEWAY_INGEST_TOKEN`。
