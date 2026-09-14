# ai-gateway DredgeAI AI 推理网关

**服务定位**：平台唯一 AI 推理网关（涉及 LLM 的必选基础套件）。ABP 的 `ILlmGateway` 与前端对话均经此转发，不直连模型。

消费 `angineer-ai-inference@v0.2.2`，对外提供 OpenAI 兼容 chat 与 SSE 流式；多模型路由、重试、熔断由库负责。
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

## 错误码

| 状态码 | code | 触发 |
|---|---|---|
| 400 | `INVALID_REQUEST` | 请求体校验失败（缺 `messages`、`mode` 非 instruct/thinking、`temperature` / `maxTokens` 越界） |
| 401 | `UNAUTHORIZED` / `PROVIDER_AUTH` | 入站令牌无效 / 上游模型鉴权失败 |
| 429 | `RATE_LIMITED` | 上游限流 |
| 500 | `INTERNAL_ERROR` | **服务端**配置或内部错误（`LLM_CONFIGS` 非法 JSON、字段非法等） |
| 502 | `LLM_TRUNCATED` / `PROVIDER_UNAVAILABLE` | 截断守卫重试后仍截断 / 供应商不可用 |
| 503 | `NO_MODELS_CONFIGURED` | `LLM_CONFIGS` 为空或未启用任何模型 |

约定：**400 只代表调用方请求体非法**，服务端配置错误一律 500，并在启动自检时先打 WARNING 日志。
（历史行为：`ANGINEER_CHAT_TEMPLATE_KWARGS` 空值/非法 JSON 在库侧抛 `ValueError`，被本网关兜底报成 400 —— 库 0.2.2 已降级 + WARNING，网关侧的 400 兜底也已移除。）
调用方（ABP `HttpLlmGateway`）仅对 5xx/408/429/超时重试，故配置类错误会被重试 3 次后失败：这是服务端错误，排障请看网关日志而非请求体。

## 环境变量

LLM 配置见 `angineer-ai-inference` 文档（`LLM_CONFIGS` + `ANGINEER_*`）；
本服务自身配置前缀 `AI_GATEWAY_`：`AI_GATEWAY_API_TOKEN`（入站校验，空=关闭）、`AI_GATEWAY_USAGE_REPORT_URL`、`AI_GATEWAY_USAGE_REPORT_ENABLED`、`AI_GATEWAY_INGEST_TOKEN`。
