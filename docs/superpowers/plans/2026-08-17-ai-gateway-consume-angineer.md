# DredgeAI 接入 angineer-ai-inference（AI Gateway 套件）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 DredgeAI 统一 AI 推理底座：新增 `services/ai-gateway` 消费 `angineer-ai-inference@v0.1.0`，ABP 后端与前端聊天全部经网关调用 LLM，用量落库并接通 admin-web「调用记录 / 用量分析」真实数据。

**Architecture:** ABP 只做业务编排，不再直连模型；所有 LLM 调用收敛到 Python 薄网关（FastAPI），网关负责多模型路由、重试、熔断、流式 SSE 与用量上报；ABP 侧 `ILlmGateway` 接口保留、实现切换为 HTTP 客户端；用量持久化由 ABP（EF Core 实体 `AiUsageRecord`）负责，网关通过 ingest 端点回传。

**Tech Stack:** Python 3.11+ / FastAPI / uv / `angineer-ai-inference@v0.1.0`（openai/pydantic/httpx）；.NET 8 / ABP / EF Core / xUnit；Vue 3 / ant-design-vue / TypeScript / vitest。

---

## 0. 背景与范围

DredgeAI 目前有三处 AI 触点：

1. **ABP 后端** `OpenAiCompatibleLlmGateway`：手写 OpenAI 兼容客户端，单模型、无重试/熔断/fallback/用量上报，被条款提取、条款响应判定、指标抽取 3 处业务调用。
2. **前端聊天** `AIChat.vue`（标准问答等使用点）：`setTimeout` 假回复。
3. **admin-web API 管理页**：模型管理/调用记录/用量分析/权限/告警，全部 mock。

`angineer-ai-inference`（v0.1.0，纯 Python 客户端）已实现 DredgeAI 此前提案的全部 P0 能力：多模型 priority 路由 + fallback、指数退避重试、每模型熔断、四段超时、`chat_result_guarded` 截断守卫、`achat_stream_events` 流式语义（delta / done / stream_failed）、错误分层、`ChatResult` 元数据。本计划把它作为平台 AI 调用的统一底座，补齐它不含的 HTTP 网关层与用量持久化层。

本计划覆盖 3 个独立子系统（网关服务 / ABP 集成 / 前端聊天与后台接线），按 Phase 顺序实现，每个 Phase 结束都可独立测试、独立提交。若希望严格按子系统拆分执行，可拆为 3 份计划，但当前单一文档更便于整体评审。

## 1. 决策记录

| # | 决策 | 理由 |
|---|---|---|
| D1 | 新建 `services/ai-gateway`（Python FastAPI），不把 LLM 逻辑塞进 `compare-algo` | compare-algo 定位「无状态确定性计算服务」；网关有密钥、限流、用量上报等独立职责 |
| D2 | ABP `ILlmGateway` 接口与 `FakeLlmGateway` 测试替身保留，仅替换实现为 `HttpLlmGateway` | 3 处业务调用点零改动，`AiAnalysisJob` 现有「AI 挂了自动降级」逻辑直接生效 |
| D3 | 用量持久化放 ABP（EF Core 实体 `AiUsageRecord`），网关经 `POST /api/ai-gateway/usage-records` 回传 | 平台唯一 DB 属主是 ABP；admin-web 用量页直接读 ABP；网关保持无状态（内存熔断器除外） |
| D4 | 网关不注册库的 `usage_callback`，改在 HTTP 层调用返回后自行上报 | `usage_callback` 拿不到业务上下文；HTTP 层有完整 `ChatResult`/done 事件，更简单可测 |
| D5 | 流式统一端点：ABP `POST /api/ai-gateway/chat/stream` 代理网关 SSE，前端 `AIChatTransport` 消费 | 前端只打 ABP（认证/CORS/代理），不直连内网网关；与既有文档「统一问答端点 + 网关 SSE 流式」一致 |
| D6 | 网关与 ABP 之间用静态令牌（`AI_GATEWAY_API_TOKEN` / `AI_GATEWAY_INGEST_TOKEN`）互验；开发环境留空即关闭 | 内网服务也需要横向最小防护，避免任意内网进程冒充 |
| D7 | 前端「标准问答/标书追问」产品未立项前不接业务数据，只交付基础设施（transport + 流式 AIChat），接入点用环境变量开关 | 与 `docs/dredgeai-ui-integration-requirements.md` §3.2/3.3 一致，避免能力空转 |
| D8 | 版本钉死 `angineer-ai-inference@v0.1.0`（git tag），不跟 main 漂移 | 库已按提案发布 v0.1.0，升级走 CHANGELOG |

## 2. 目标架构

```mermaid
flowchart LR
  subgraph DredgeAI
    ABP[ABP 后端<br/>ILlmGateway -> HttpLlmGateway<br/>AiUsageRecord + 管理端点]
    Front[user-web / admin-web]
  end
  G[services/ai-gateway<br/>FastAPI 薄网关]
  L[angineer-ai-inference@v0.1.0<br/>路由/重试/熔断/流式/截断守卫]
  P1[Qwen / DeepSeek ...]
  P2[OpenAI 兼容端点]

  ABP -->|POST /v1/chat| G
  Front -->|POST /api/ai-gateway/chat/stream| ABP
  ABP -->|SSE 透传| Front
  G --> L
  L --> P1
  L --> P2
  G -->|POST /api/ai-gateway/usage-records| ABP
  ABP -->|GET /api/*/apikey/*| Front
```

## 3. 契约

### 3.1 ai-gateway 对外 API（新增服务，端口 8200）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/healthz` | 健康检查，返回 `{"status":"ok"}` |
| GET | `/v1/models` | 模型配置列表（`api_key` 已脱敏为 `***`），来自 `LLM_CONFIGS` |
| POST | `/v1/chat` | 非流式对话，内部走 `achat_result_guarded` |
| POST | `/v1/chat/stream` | SSE 流式对话，内部走 `achat_stream_events` |

`POST /v1/chat` 请求体（camelCase）：

```json
{
  "messages": [{ "role": "system", "content": "..." }, { "role": "user", "content": "..." }],
  "mode": "instruct",
  "configName": null,
  "temperature": null,
  "maxTokens": null,
  "business": "bid-compare"
}
```

`mode` 仅允许 `instruct` / `thinking`；`messages` 至少 1 条。成功响应 `200`：

```json
{
  "text": "...",
  "finishReason": "stop",
  "usage": { "prompt_tokens": 10, "completion_tokens": 20, "total_tokens": 30 },
  "usedConfig": "Qwen3.6-A3B",
  "usedModel": "Qwen3.6-35B-A3B-FP8",
  "attempts": 1,
  "latencySeconds": 0.53,
  "circuitBreakerState": "closed"
}
```

`POST /v1/chat/stream` 以 `text/event-stream` 返回，事件负载为 JSON 行（`data: {...}\n\n`）：

| type | 字段 | 含义 |
|---|---|---|
| `delta` | `text` | 增量文本 |
| `done` | `finishReason` / `usage` / `usedConfig` / `usedModel` / `attempts` / `latencySeconds` / `circuitBreakerState` | 正常结束 |
| `stream_failed` | `text`（已输出部分）/ `error:{type,message}` / 元数据 | 已产出 delta 后失败，停止流 |
| `error` | `error:{type,message}` | 首个 delta 之前失败（库抛错），终止流 |

### 3.2 ai-gateway 错误契约（非 SSE 端点）

```json
{ "code": "PROVIDER_UNAVAILABLE", "message": "...", "details": null }
```

| HTTP | code | 来源 |
|---|---|---|
| 400 | `INVALID_REQUEST` | 请求体校验失败 / 库 `ValueError` |
| 401 | `UNAUTHORIZED` | 网关令牌错误（配置了 `AI_GATEWAY_API_TOKEN` 时） |
| 401 | `PROVIDER_AUTH` | `ProviderAuthError` |
| 429 | `RATE_LIMITED` | `RateLimitedError` |
| 502 | `LLM_TRUNCATED` | `LLMTruncatedError`（截断守卫重试后仍截断） |
| 502 | `PROVIDER_UNAVAILABLE` | `ProviderUnavailableError` / `AllProvidersFailedError` / `LLMStreamError` |
| 500 | `INTERNAL_ERROR` | 未预期异常 |

### 3.3 ABP 新增 API（遵循 `.opencode/rules/abp-api-conventions.md`）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/ai-gateway/usage-records` | 网关用量上报（要求 `X-Gateway-Token`，配置了 `AiGateway:IngestToken` 时） |
| POST | `/api/ai-gateway/chat/stream` | 前端统一问答端点，SSE 透传网关 |
| GET | `/api/admin/apikey/usage-stats` | admin-web 用量汇总 `{totalCalls, totalTokens}` |
| GET | `/api/admin/apikey/usage-timeseries` | admin-web 用量时序 `{categories, byModel, byKey, byName}` |
| GET | `/api/admin/apikey/records` | admin-web 调用记录（`PagedResultDto<AiUsageRecordDto>`） |
| GET | `/api/apikey/usage-stats` / `usage-timeseries` / `records` | user-web API 页同构端点 |

以上接口全部按 ABP 约定：camelCase、UTC ISO 8601、`PagedResultDto`、`RemoteServiceErrorResponse` 错误体。

### 3.4 环境变量（新增）

| 变量 | 默认 | 说明 |
|---|---|---|
| `LLM_CONFIGS` | 空 | JSON 数组，多模型配置（由 ai-inference 读取） |
| `ANGINEER_*` | 见库文档 | 超时/重试/熔断参数（由 ai-inference 读取） |
| `AI_GATEWAY_API_TOKEN` | 空 | 网关入站校验（ABP/调用方带 `X-API-Key`；空=关闭） |
| `AI_GATEWAY_USAGE_REPORT_URL` | `http://localhost:44361/api/ai-gateway/usage-records` | 用量上报目标 |
| `AI_GATEWAY_USAGE_REPORT_ENABLED` | `true` | 上报开关 |
| `AI_GATEWAY_INGEST_TOKEN` | 空 | 上报时携带 `X-Gateway-Token` |
| `AI_GATEWAY_BASE_URL` | `http://localhost:8200` | ABP → 网关地址 |
| `AI_GATEWAY_API_TOKEN`（ABP 侧同名） | 空 | ABP → 网关时携带 `X-API-Key` |
| `AI_GATEWAY_INGEST_TOKEN`（ABP 侧同名） | 空 | 校验网关上报的 `X-Gateway-Token` |

> 命名冲突说明：`AI_GATEWAY_API_TOKEN` / `AI_GATEWAY_INGEST_TOKEN` 在网关服务和 ABP 进程各有一份，含义对称（出站/入站），分属不同进程的 env，互不覆盖。

## 4. 文件结构

### 4.1 新增文件

| 路径 | 职责 |
|---|---|
| `services/ai-gateway/pyproject.toml` | uv 工程、依赖（钉 `angineer-ai-inference@v0.1.0`） |
| `services/ai-gateway/README.md` | 启动/测试/接口说明 |
| `services/ai-gateway/.gitignore` | 忽略 `.venv`、`__pycache__`、`.pytest_cache` |
| `services/ai-gateway/app/__init__.py` | 包标记 |
| `services/ai-gateway/app/settings.py` | `AI_GATEWAY_*` 配置 |
| `services/ai-gateway/app/schemas.py` | `ChatRequest` / `ChatResponse` / `ErrorResponse` |
| `services/ai-gateway/app/errors.py` | 库异常 → HTTP 状态码/错误码映射 |
| `services/ai-gateway/app/usage.py` | 用量上报（httpx → ABP ingest，后台任务） |
| `services/ai-gateway/app/main.py` | FastAPI 入口 + 4 个端点 + 统一错误处理 |
| `services/ai-gateway/tests/conftest.py` | `FakeLLMClient` + 注册 fixture |
| `services/ai-gateway/tests/test_api.py` | healthz/models/chat/stream/错误/用量测试 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AI/AiGatewayOptions.cs` | 网关连接配置 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AI/HttpLlmGateway.cs` | `ILlmGateway` 网关 HTTP 实现 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Domain/AI/AiUsageRecord.cs` | 用量实体 |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application.Contracts/AI/AiUsageRecordDtos.cs` | 用量 DTO（Dto / CreateDto / GetInput） |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/AI/AiUsageRecordAppService.cs` | 用量服务（create/list/stats/timeseries） |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/Controllers/AiGatewayController.cs` | usage-records ingest（需 `AiGatewayOptions`，仅 Host 可引用） |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/Controllers/AiGatewayChatController.cs` | chat/stream SSE 代理（需 `OwnedStream`，仅 Host 可引用） |
| `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi/Controllers/ApiKeyController.cs` | `/api/admin/apikey/*` 与 `/api/apikey/*` |
| `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/AI/HttpLlmGatewayTests.cs` | 网关客户端测试 |
| `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/AI/AiUsageRecordAppServiceTests.cs` | 用量服务测试 |
| `packages/shared/src/web/chat/transport.ts` | SSE 传输（fetch + ReadableStream） |
| `packages/shared/src/web/composables/useAIChat.ts` | 聊天状态组合式函数 |
| `user-web/__tests__/ai-chat-transport.test.ts` | SSE 解析测试 |

### 4.2 修改文件

| 路径 | 修改点 |
|---|---|
| `services/ai-gateway/`（无既有文件） | — |
| `backend/.../BidCompareHttpApiHostModule.cs` | 注册 `AiGatewayOptions` + `HttpLlmGateway` 命名 HttpClient |
| `backend/.../HttpApi.Host/Program.cs` | `.env` 映射改为 `AI_GATEWAY_*`，DEBUG 日志改读 `AiGatewayOptions` |
| `backend/.../HttpApi.Host/appsettings.json` | `Llm` 节 → `AiGateway` 节 |
| `backend/.../Domain.Shared/BidCompareErrorCodes.cs` | 新增 `AiGatewayFailed` |
| `backend/.../EntityFrameworkCore/BidCompareDbContext.cs` | `DbSet<AiUsageRecord>` + 表映射 |
| `backend/.../Application/BidCompareApplicationAutoMapperProfile.cs` | `AiUsageRecord → AiUsageRecordDto` |
| `start.ps1` | 启动/清理/健康检查/汇总加入 ai-gateway（8200） |
| `README.md` | 服务表、环境变量、流程说明 |
| `packages/shared/src/core/types/chat.ts` | 扩展聊天类型（ChatRequest/ChatResult/ChatStreamEvent） |
| `packages/shared/src/web/components/AIChat.vue` | 流式文本 + 错误展示 |
| `packages/shared/src/web/index.ts` | 导出 transport / composable |
| `admin-web/src/api/modules/apikey.ts` | 新增 `getUsageRecords` |
| `admin-web/src/views/api/index.vue` | 调用记录改走真实接口 |
| `user-web/src/views/standards/components/StandardProperty.vue` | 可选：环境变量开关接通流式问答 |

## 5. Phase 1：ai-gateway 骨架（healthz + models）

### Task 1.1：创建 uv 工程骨架

**Files:**
- Create: `services/ai-gateway/pyproject.toml`
- Create: `services/ai-gateway/.gitignore`
- Create: `services/ai-gateway/README.md`
- Create: `services/ai-gateway/app/__init__.py`

- [ ] **Step 1：创建 `pyproject.toml`**

```toml
[project]
name = "ai-gateway"
version = "0.1.0"
description = "DredgeAI AI 推理网关：消费 angineer-ai-inference，提供 chat / SSE 流式，负责用量上报。"
requires-python = ">=3.11"
dependencies = [
    "fastapi>=0.115",
    "uvicorn>=0.30",
    "pydantic>=2.7",
    "pydantic-settings>=2.0",
    "angineer-ai-inference @ git+https://github.com/0mao0/angineer-ai-inference.git@v0.1.0",
]

[dependency-groups]
dev = [
    "pytest>=8.0",
    "httpx>=0.27",
]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
```

- [ ] **Step 2：创建 `.gitignore`**

```gitignore
.venv/
__pycache__/
.pytest_cache/
*.pyc
```

- [ ] **Step 3：创建 `README.md`**

```markdown
# ai-gateway DredgeAI AI 推理网关

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
本服务自身配置前缀 `AI_GATEWAY_`：`AI_GATEWAY_API_TOKEN`（入站校验，空=关闭）、
`AI_GATEWAY_USAGE_REPORT_URL`、`AI_GATEWAY_USAGE_REPORT_ENABLED`、`AI_GATEWAY_INGEST_TOKEN`。
```

- [ ] **Step 4：创建 `app/__init__.py`（空文件）**

- [ ] **Step 5：安装依赖并验证**

Run:
```powershell
cd services\ai-gateway
uv sync
uv run python -c "from ai_inference import LLMClient, achat_result_guarded; print('ok')"
```
Expected: `ok`（若本机无 `uv`，改用 `python -m venv .venv` + `.venv\Scripts\pip install -e ".[dev]"`）。

- [ ] **Step 6：Commit**

```bash
git add services/ai-gateway
git commit -m "feat(ai-gateway): scaffold uv project consuming angineer-ai-inference@v0.1.0"
```

### Task 1.2：settings / schemas / errors / usage 基础模块

**Files:**
- Create: `services/ai-gateway/app/settings.py`
- Create: `services/ai-gateway/app/schemas.py`
- Create: `services/ai-gateway/app/errors.py`
- Create: `services/ai-gateway/app/usage.py`

- [ ] **Step 1：创建 `settings.py`**

```python
"""集中配置：全部经 pydantic-settings 管理，环境变量前缀 AI_GATEWAY_。"""
from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="AI_GATEWAY_")

    # 入站校验令牌；空表示关闭（开发环境）
    api_token: str = ""
    # 用量上报（ABP ingest 端点）
    usage_report_url: str = "http://localhost:44361/api/ai-gateway/usage-records"
    usage_report_enabled: bool = True
    ingest_token: str = ""


@lru_cache
def get_settings() -> Settings:
    """进程级配置单例；调用时读取，便于测试 monkeypatch 后 cache_clear。"""
    return Settings()
```

- [ ] **Step 2：创建 `schemas.py`**

```python
from typing import Any

from pydantic import BaseModel, Field, field_validator


class ChatMessage(BaseModel):
    role: str
    content: Any = None


class ChatRequest(BaseModel):
    messages: list[ChatMessage] = Field(min_length=1)
    mode: str | None = None
    config_name: str | None = None
    temperature: float | None = Field(default=None, ge=0.0, le=2.0)
    max_tokens: int | None = Field(default=None, ge=1)
    business: str = "general"

    @field_validator("mode")
    @classmethod
    def _check_mode(cls, v: str | None) -> str | None:
        if v is not None and v not in ("instruct", "thinking"):
            raise ValueError("mode 仅支持 instruct / thinking")
        return v


class ChatResponse(BaseModel):
    text: str
    finish_reason: str | None = None
    usage: dict[str, Any] | None = None
    used_config: str | None = None
    used_model: str | None = None
    attempts: int = 1
    latency_seconds: float | None = None
    circuit_breaker_state: str | None = None


class ErrorResponse(BaseModel):
    code: str
    message: str
    details: dict[str, Any] | None = None
```

- [ ] **Step 3：创建 `errors.py`**

```python
"""库异常 -> HTTP 状态码/错误码映射；SSE 端点复用同一映射产出 error 事件。"""
from ai_inference.errors import (
    AllProvidersFailedError,
    LLMStreamError,
    LLMTruncatedError,
    ProviderAuthError,
    ProviderUnavailableError,
    RateLimitedError,
)


def error_status(exc: Exception) -> tuple[int, str]:
    if isinstance(exc, ProviderAuthError):
        return 401, "PROVIDER_AUTH"
    if isinstance(exc, RateLimitedError):
        return 429, "RATE_LIMITED"
    if isinstance(exc, LLMTruncatedError):
        return 502, "LLM_TRUNCATED"
    if isinstance(exc, (ProviderUnavailableError, AllProvidersFailedError, LLMStreamError)):
        return 502, "PROVIDER_UNAVAILABLE"
    if isinstance(exc, ValueError):
        return 400, "INVALID_REQUEST"
    return 500, "INTERNAL_ERROR"


class LlmHttpError(Exception):
    def __init__(self, status_code: int, code: str, message: str, details: dict | None = None):
        super().__init__(message)
        self.status_code = status_code
        self.code = code
        self.message = message
        self.details = details
```

- [ ] **Step 4：创建 `usage.py`**

```python
"""用量上报：fire-and-forget POST 到 ABP ingest 端点；失败仅记 warning，不影响业务响应。"""
import asyncio
import logging
from datetime import datetime, timezone

import httpx

from app.settings import get_settings

logger = logging.getLogger("ai-gateway")

_tasks: set[asyncio.Task] = set()


def _spawn(coro) -> None:
    task = asyncio.create_task(coro)
    _tasks.add(task)
    task.add_done_callback(_tasks.discard)


def usage_payload(
    *,
    business: str,
    text: str,
    finish_reason: str | None,
    usage: dict | None,
    used_config: str | None,
    used_model: str | None,
    attempts: int,
    latency_seconds: float | None,
    circuit_breaker_state: str | None,
    success: bool,
    error_type: str | None = None,
    error_message: str | None = None,
) -> dict:
    return {
        "business": business,
        "usedConfig": used_config,
        "usedModel": used_model,
        "inputTokens": (usage or {}).get("prompt_tokens"),
        "outputTokens": (usage or {}).get("completion_tokens"),
        "totalTokens": (usage or {}).get("total_tokens"),
        "finishReason": finish_reason,
        "attempts": attempts,
        "latencySeconds": latency_seconds,
        "circuitBreakerState": circuit_breaker_state,
        "success": success,
        "errorType": error_type,
        "errorMessage": error_message,
        "requestedAt": datetime.now(timezone.utc).isoformat(),
        "textPreview": text[:200],
    }


def enqueue_usage(payload: dict) -> None:
    _spawn(report_usage(payload))


async def report_usage(payload: dict) -> None:
    settings = get_settings()
    if not settings.usage_report_enabled:
        return
    headers = {}
    if settings.ingest_token:
        headers["X-Gateway-Token"] = settings.ingest_token
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            response = await client.post(settings.usage_report_url, json=payload, headers=headers)
            response.raise_for_status()
    except Exception as exc:  # noqa: BLE001 - 上报失败不影响主链路
        logger.warning("usage report failed: %s", exc)
```

> 说明：`textPreview` 仅用于联调排查，前端/报表不展示；若未来有隐私顾虑可去掉。

- [ ] **Step 5：Commit**

```bash
git add services/ai-gateway/app
git commit -m "feat(ai-gateway): add settings, schemas, error mapping and usage reporter"
```

### Task 1.3：FakeLLMClient + healthz/models 测试（TDD）

**Files:**
- Create: `services/ai-gateway/tests/conftest.py`
- Create: `services/ai-gateway/tests/test_api.py`

- [ ] **Step 1：创建 `tests/conftest.py`**

```python
import pytest
from ai_inference import ChatResult


class FakeLLMClient:
    """与 ai_inference.LLMClient 的 achat_result / achat_stream_events 契约一致的最小替身。"""

    def __init__(self, *, result: ChatResult | None = None, stream_events: list[dict] | None = None, error: Exception | None = None):
        self.result = result or ChatResult(
            text="ok",
            finish_reason="stop",
            usage={"prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15},
            attempts=1,
            latency_seconds=0.01,
            used_config="fake",
            used_model="fake-model",
            circuit_breaker_state="closed",
        )
        self.stream_events = stream_events
        self.error = error
        self.calls: list[dict] = []

    @property
    def configs(self) -> list[dict]:
        return [{
            "name": "fake",
            "model": "fake-model",
            "api_key": "***",
            "base_url": "http://fake/v1",
            "enabled": True,
            "priority": 1,
        }]

    async def achat_result(self, messages, temperature=None, model=None, mode="instruct",
                           config_name=None, max_tokens=None, tools=None):
        self.calls.append({"messages": messages, "mode": mode, "config_name": config_name})
        if self.error is not None:
            raise self.error
        return self.result

    async def achat_stream_events(self, messages, temperature=None, model=None, mode="instruct",
                                  config_name=None, max_tokens=None, tools=None):
        self.calls.append({"messages": messages, "mode": mode, "config_name": config_name})
        if self.error is not None:
            raise self.error
        for event in self.stream_events or []:
            yield event


@pytest.fixture()
def fake_client(monkeypatch):
    client = FakeLLMClient()
    # 网关自持 client 实例（模块级 _client），测试只替换访问器
    monkeypatch.setattr("app.main.llm_client", lambda: client)
    return client
```

- [ ] **Step 2：创建 `tests/test_api.py`（第一批：healthz + models）**

```python
from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_healthz():
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_models(fake_client):
    r = client.get("/v1/models")
    assert r.status_code == 200
    assert r.json()["models"] == fake_client.configs
```

- [ ] **Step 3：运行测试，确认失败（main 尚不存在）**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
```
Expected: FAIL，`ModuleNotFoundError: No module named 'app.main'`。

### Task 1.4：实现 main.py（healthz + models + 统一错误处理）

**Files:**
- Create: `services/ai-gateway/app/main.py`

- [ ] **Step 1：创建 `app/main.py`**

```python
"""ai-gateway FastAPI 入口：healthz / models / chat / chat/stream + 统一错误处理。"""
import logging
import threading

from ai_inference import LLMClient, load_llm_config_from_env
from fastapi import Depends, FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from app.errors import LlmHttpError, error_status
from app.schemas import ErrorResponse
from app.settings import get_settings

logger = logging.getLogger("ai-gateway")


def _configure_logging() -> None:
    root = logging.getLogger()
    if root.handlers:
        return
    handler = logging.StreamHandler()
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s [%(name)s] %(message)s"))
    root.addHandler(handler)
    root.setLevel(logging.INFO)


_configure_logging()

app = FastAPI(title="ai-gateway", version="0.1.0")


def require_api_token(request: Request) -> None:
    token = get_settings().api_token
    if token and request.headers.get("X-API-Key") != token:
        raise LlmHttpError(401, "UNAUTHORIZED", "无效的网关令牌")


_client: LLMClient | None = None
_client_lock = threading.Lock()


def llm_client() -> LLMClient:
    """进程内单例：由 env（LLM_CONFIGS + ANGINEER_*）构造；测试通过 monkeypatch 本函数替换。"""
    global _client
    if _client is None:
        with _client_lock:
            if _client is None:
                _client = LLMClient(load_llm_config_from_env())
    return _client


@app.exception_handler(LlmHttpError)
async def llm_http_error_handler(request: Request, exc: LlmHttpError) -> JSONResponse:
    return JSONResponse(
        status_code=exc.status_code,
        content=ErrorResponse(code=exc.code, message=exc.message, details=exc.details).model_dump(),
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError) -> JSONResponse:
    return JSONResponse(
        status_code=400,
        content=ErrorResponse(
            code="INVALID_REQUEST",
            message="请求体校验失败",
            details={"errors": exc.errors()},
        ).model_dump(),
    )


@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError) -> JSONResponse:
    status, code = error_status(exc)
    return JSONResponse(
        status_code=status,
        content=ErrorResponse(code=code, message=str(exc)).model_dump(),
    )


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    logger.exception("unhandled error on %s: %s", request.url.path, exc)
    return JSONResponse(
        status_code=500,
        content=ErrorResponse(code="INTERNAL_ERROR", message="内部错误").model_dump(),
    )


@app.get("/healthz")
def healthz() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/v1/models", dependencies=[Depends(require_api_token)])
def get_models() -> dict:
    return {"models": llm_client().configs}
```

- [ ] **Step 2：运行测试，确认通过**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
```
Expected: `2 passed`。

- [ ] **Step 3：Commit**

```bash
git add services/ai-gateway/app services/ai-gateway/tests
git commit -m "feat(ai-gateway): add healthz and models endpoints"
```

## 6. Phase 2：POST /v1/chat

### Task 2.1：chat 端点测试（TDD）

**Files:**
- Modify: `services/ai-gateway/tests/test_api.py`

- [ ] **Step 1：追加 chat 测试**

```python
import pytest
from ai_inference import (
    AllProvidersFailedError,
    ChatResult,
    ProviderAuthError,
    RateLimitedError,
)


def test_chat_success(fake_client):
    r = client.post("/v1/chat", json={
        "messages": [{"role": "user", "content": "你好"}],
        "mode": "thinking",
        "business": "standard-qa",
    })
    assert r.status_code == 200
    body = r.json()
    assert body["text"] == "ok"
    assert body["usedConfig"] == "fake"
    assert body["attempts"] == 1
    assert fake_client.calls[0]["mode"] == "thinking"


@pytest.mark.parametrize("error,status,code", [
    (ProviderAuthError("bad key"), 401, "PROVIDER_AUTH"),
    (RateLimitedError("429"), 429, "RATE_LIMITED"),
    (AllProvidersFailedError("all down"), 502, "PROVIDER_UNAVAILABLE"),
])
def test_chat_error_mapping(fake_client, error, status, code):
    fake_client.error = error
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == status
    assert r.json()["code"] == code


def test_chat_truncated_maps_502(fake_client):
    # 截断守卫重试一次仍截断 -> LLMTruncatedError -> 502 LLM_TRUNCATED
    fake_client.result = ChatResult(
        text="partial", finish_reason="length",
        attempts=1, latency_seconds=0.1, used_config="fake", used_model="fake-model",
    )
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "x" * 10}]})
    assert r.status_code == 502
    assert r.json()["code"] == "LLM_TRUNCATED"


def test_chat_invalid_body():
    r = client.post("/v1/chat", json={"messages": []})
    assert r.status_code == 400
    assert r.json()["code"] == "INVALID_REQUEST"


def test_chat_no_models_503(fake_client, monkeypatch):
    class EmptyClient:
        @property
        def configs(self) -> list[dict]:
            return []
    monkeypatch.setattr("app.main.llm_client", lambda: EmptyClient())
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 503
    assert r.json()["code"] == "NO_MODELS_CONFIGURED"


def test_chat_reports_usage(fake_client, monkeypatch):
    reported: list[dict] = []
    # main 模块以 `from app.usage import enqueue_usage` 引用，须替换 main 命名空间内的名字
    monkeypatch.setattr("app.main.enqueue_usage", reported.append)
    r = client.post("/v1/chat", json={
        "messages": [{"role": "user", "content": "hi"}],
        "business": "bid-compare",
    })
    assert r.status_code == 200
    assert reported[0]["business"] == "bid-compare"
    assert reported[0]["usedConfig"] == "fake"
    assert reported[0]["totalTokens"] == 15
    assert reported[0]["success"] is True
```

- [ ] **Step 2：运行测试，确认失败**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
```
Expected: 新增用例 FAIL（`POST /v1/chat` 返回 404）。

### Task 2.2：实现 /v1/chat

**Files:**
- Modify: `services/ai-gateway/app/main.py`

- [ ] **Step 1：在 `main.py` 追加 chat 端点与用量上报**

```python
from ai_inference import achat_result_guarded
from ai_inference.errors import LLMError

from app.schemas import ChatRequest, ChatResponse
from app.usage import enqueue_usage, usage_payload


def _require_models() -> None:
    if not llm_client().configs:
        raise LlmHttpError(503, "NO_MODELS_CONFIGURED", "LLM_CONFIGS 为空或未启用任何模型")


@app.post("/v1/chat", response_model=ChatResponse, dependencies=[Depends(require_api_token)])
async def post_chat(req: ChatRequest) -> ChatResponse:
    _require_models()
    messages = [m.model_dump() for m in req.messages]
    try:
        result = await achat_result_guarded(
            llm_client(),
            messages,
            mode=req.mode or "instruct",
            config_name=req.config_name,
            temperature=req.temperature,
            max_tokens=req.max_tokens,
        )
    except LLMError as exc:
        status, code = error_status(exc)
        raise LlmHttpError(status, code, str(exc)) from exc

    enqueue_usage(usage_payload(
        business=req.business,
        text=result.text or "",
        finish_reason=result.finish_reason,
        usage=result.usage,
        used_config=result.used_config,
        used_model=result.used_model,
        attempts=result.attempts or 1,
        latency_seconds=result.latency_seconds,
        circuit_breaker_state=result.circuit_breaker_state,
        success=True,
    ))
    return ChatResponse(
        text=result.text or "",
        finish_reason=result.finish_reason,
        usage=result.usage,
        used_config=result.used_config,
        used_model=result.used_model,
        attempts=result.attempts or 1,
        latency_seconds=result.latency_seconds,
        circuit_breaker_state=result.circuit_breaker_state,
    )
```

> 提示：把新增 `from app.usage import ...` 等 import 放到文件顶部既有 import 之后；本文件不直接使用 `asyncio`（用量任务由 `usage.enqueue_usage` 内部 spawn）。

- [ ] **Step 2：运行测试，确认通过**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
```
Expected: `10 passed`。

- [ ] **Step 3：Commit**

```bash
git add services/ai-gateway/app services/ai-gateway/tests
git commit -m "feat(ai-gateway): add POST /v1/chat with error mapping and usage report"
```

## 7. Phase 3：POST /v1/chat/stream（SSE）

### Task 3.1：流式测试（TDD）

**Files:**
- Modify: `services/ai-gateway/tests/test_api.py`

- [ ] **Step 1：追加流式测试**

```python
def test_stream_delta_and_done(fake_client):
    fake_client.stream_events = [
        {"type": "delta", "text": "你"},
        {"type": "delta", "text": "好"},
        {
            "type": "done",
            "finish_reason": "stop",
            "usage": {"prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15},
            "used_config": "fake",
            "used_model": "fake-model",
            "attempts": 1,
            "latency_seconds": 0.01,
            "circuit_breaker_state": "closed",
        },
    ]
    r = client.post("/v1/chat/stream", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/event-stream")
    lines = [ln for ln in r.text.splitlines() if ln.startswith("data: ")]
    payloads = [line[6:] for line in lines]
    assert '"type": "delta"' in payloads[0] and '"text": "你"' in payloads[0]
    assert '"type": "done"' in payloads[-1] and '"finishReason": "stop"' in payloads[-1]


def test_stream_error_before_first_delta(fake_client):
    fake_client.error = ProviderAuthError("bad key")
    r = client.post("/v1/chat/stream", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/event-stream")
    assert '"type": "error"' in r.text
    assert '"PROVIDER_AUTH"' in r.text


def test_stream_failed_after_partial(fake_client):
    fake_client.stream_events = [
        {"type": "delta", "text": "部分"},
        {
            "type": "stream_failed",
            "text": "部分",
            "finish_reason": None,
            "error": {"type": "LLMStreamError", "message": "中断"},
            "used_config": "fake",
            "used_model": "fake-model",
            "attempts": 2,
            "latency_seconds": 1.2,
            "circuit_breaker_state": "closed",
        },
    ]
    r = client.post("/v1/chat/stream", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 200
    assert '"type": "stream_failed"' in r.text
    assert '"text": "部分"' in r.text
```

- [ ] **Step 2：运行测试，确认失败**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
```
Expected: 新增用例 FAIL（404）。

### Task 3.2：实现 /v1/chat/stream

**Files:**
- Modify: `services/ai-gateway/app/main.py`

- [ ] **Step 1：在 `main.py` 追加 SSE 端点**

```python
import json

from fastapi.responses import StreamingResponse


def _sse(event: dict) -> str:
    return f"data: {json.dumps(event, ensure_ascii=False)}\n\n"


@app.post("/v1/chat/stream", dependencies=[Depends(require_api_token)])
async def post_chat_stream(req: ChatRequest) -> StreamingResponse:
    _require_models()
    messages = [m.model_dump() for m in req.messages]

    async def generate():
        try:
            async for event in llm_client().achat_stream_events(
                messages,
                mode=req.mode or "instruct",
                config_name=req.config_name,
                temperature=req.temperature,
                max_tokens=req.max_tokens,
            ):
                if event["type"] == "done":
                    enqueue_usage(usage_payload(
                        business=req.business,
                        text="",
                        finish_reason=event.get("finish_reason"),
                        usage=event.get("usage"),
                        used_config=event.get("used_config"),
                        used_model=event.get("used_model"),
                        attempts=event.get("attempts") or 1,
                        latency_seconds=event.get("latency_seconds"),
                        circuit_breaker_state=event.get("circuit_breaker_state"),
                        success=True,
                    ))
                yield _sse(event)
        except LLMError as exc:
            status, code = error_status(exc)
            yield _sse({"type": "error", "error": {"type": code, "message": str(exc)}})

    return StreamingResponse(
        generate(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
```

- [ ] **Step 2：运行测试，确认通过**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
```
Expected: `13 passed`。

- [ ] **Step 3：Commit**

```bash
git add services/ai-gateway/app services/ai-gateway/tests
git commit -m "feat(ai-gateway): add SSE POST /v1/chat/stream"
```

## 8. Phase 4：ABP 切换 ILlmGateway 到网关

### Task 4.1：AiGatewayOptions + 配置迁移

**Files:**
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AI/AiGatewayOptions.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/appsettings.json`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/BidCompareHttpApiHostModule.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/Program.cs`
- Delete: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AI/OpenAiCompatibleLlmGateway.cs`
- Delete: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AI/LlmOptions.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Domain.Shared/BidCompareErrorCodes.cs`

- [ ] **Step 1：创建 `AiGatewayOptions.cs`**

```csharp
namespace DredgeAI.BidCompare.AI;

public class AiGatewayOptions
{
    /// <summary>services/ai-gateway 基地址，如 http://localhost:8200。</summary>
    public string BaseUrl { get; set; } = "http://localhost:8200";

    /// <summary>ABP -> 网关的入站令牌（X-API-Key）；空表示开发环境不校验。</summary>
    public string ApiToken { get; set; } = "";

    /// <summary>校验网关 -> ABP 用量上报的令牌（X-Gateway-Token）；空表示开发环境不校验。</summary>
    public string IngestToken { get; set; } = "";

    /// <summary>单次请求超时（秒）；流式由库的四段超时控制，此处为 HTTP 总上限。</summary>
    public int TimeoutSeconds { get; set; } = 120;
}
```

- [ ] **Step 2：修改 `appsettings.json`：`Llm` 节替换为 `AiGateway` 节**

```json
  "AiGateway": {
    "BaseUrl": "http://localhost:8200",
    "ApiToken": "",
    "IngestToken": "",
    "TimeoutSeconds": 120
  },
```

- [ ] **Step 3：修改 `BidCompareHttpApiHostModule.cs`：替换注册**

删除：
```csharp
        Configure<LlmOptions>(configuration.GetSection("Llm"));
```

替换为：
```csharp
        Configure<AiGatewayOptions>(configuration.GetSection("AiGateway"));
```

并在 `ConfigureServices` 的 HttpClient 注册区（`AddHttpClient(nameof(HttpCompareAlgoClient), ...)` 之后）追加：
```csharp
        context.Services.AddHttpClient(nameof(HttpLlmGateway), (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AiGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
```

- [ ] **Step 4：修改 `Program.cs`：env 映射与 DEBUG 日志**

删除两行旧映射：
```csharp
        MapEnv(overrides, "LLM_API_KEY", "Llm:ApiKey");
        MapEnv(overrides, "LLM_ENDPOINT", "Llm:Endpoint");
        MapEnv(overrides, "LLM_MODEL", "Llm:Model");
```

替换为：
```csharp
        MapEnv(overrides, "AI_GATEWAY_BASE_URL", "AiGateway:BaseUrl");
        MapEnv(overrides, "AI_GATEWAY_API_TOKEN", "AiGateway:ApiToken");
        MapEnv(overrides, "AI_GATEWAY_INGEST_TOKEN", "AiGateway:IngestToken");
```

DEBUG 日志块替换为：
```csharp
#if DEBUG
            var aiGatewayOptions = app.Services.GetRequiredService<IOptions<AiGatewayOptions>>().Value;
            Log.Information("AI Gateway config: baseUrl={BaseUrl}, apiTokenSet={ApiTokenSet}",
                aiGatewayOptions.BaseUrl, !string.IsNullOrWhiteSpace(aiGatewayOptions.ApiToken));
#endif
```

同时把 `using DredgeAI.BidCompare.AI;` 保留（已在文件顶部），删除对 `LlmOptions` 的引用。

- [ ] **Step 5：删除旧文件并新增错误码**

删除 `OpenAiCompatibleLlmGateway.cs` 与 `LlmOptions.cs`；在 `BidCompareErrorCodes.cs` 追加：
```csharp
    public const string AiGatewayFailed = Namespace + "AiGatewayFailed";
```

- [ ] **Step 6：编译验证**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet build src\DredgeAI.BidCompare.HttpApi.Host -v q
```
Expected: `Build succeeded`（此时 `ILlmGateway` 暂无实现，如编译报错说明有遗漏引用，按报错修正）。

- [ ] **Step 7：Commit**

```bash
git add backend/DredgeAI.BidCompare/src
git commit -m "feat(abp): replace Llm options with AiGateway options"
```

### Task 4.2：HttpLlmGateway 实现

**Files:**
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AI/HttpLlmGateway.cs`

- [ ] **Step 1：创建 `HttpLlmGateway.cs`**

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.AI;

/// <summary>
/// 通过 services/ai-gateway 调用 LLM：多模型路由、重试、熔断、截断守卫均由网关承载；
/// 本客户端只负责 HTTP 封装与错误透传（5xx/408/429/超时按 TransientHttpRetry 重试）。
/// </summary>
public class HttpLlmGateway : ILlmGateway, ITransientDependency
{
    private const int MaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiGatewayOptions _options;
    private readonly ILogger<HttpLlmGateway> _logger;

    public HttpLlmGateway(
        IHttpClientFactory httpClientFactory,
        IOptions<AiGatewayOptions> options,
        ILogger<HttpLlmGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        }

        var request = new
        {
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            mode = "instruct",
            business = "bid-compare"
        };

        using var response = await TransientHttpRetry.ExecuteAsync(
            async ct => await client.PostAsJsonAsync("v1/chat", request, JsonOptions, ct),
            _logger,
            "AI Gateway /v1/chat",
            MaxAttempts,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildGatewayExceptionAsync(response, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cancellationToken);
        return payload?.Text
            ?? throw new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
                .WithData("reason", "AI Gateway 响应缺少 text");
    }

    private static async Task<BusinessException> BuildGatewayExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? code = null;
        string? message = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("code", out var c)) code = c.GetString();
            if (document.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
        }
        catch (JsonException)
        {
            // 非 JSON 错误体：原样摘录
        }
        return new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
            .WithData("statusCode", (int)response.StatusCode)
            .WithData("serviceCode", code ?? "")
            .WithData("message", message ?? (body.Length <= 512 ? body : body[..512]));
    }

    private class ChatResponse
    {
        public string? Text { get; set; }
        public string? FinishReason { get; set; }
        public JsonElement? Usage { get; set; }
        public string? UsedConfig { get; set; }
        public string? UsedModel { get; set; }
        public int? Attempts { get; set; }
        public double? LatencySeconds { get; set; }
        public string? CircuitBreakerState { get; set; }
    }
}
```

- [ ] **Step 2：编译验证**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet build src\DredgeAI.BidCompare.HttpApi.Host -v q
```
Expected: `Build succeeded`。

### Task 4.3：HttpLlmGateway 测试

**Files:**
- Create: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/AI/HttpLlmGatewayTests.cs`

> Application.Tests 已引用 HttpApi.Host（见其 csproj），可直接测试 `HttpLlmGateway`；现有 `FakeLlmGateway` 替换注册不受影响。

- [ ] **Step 1：创建测试（stub HttpMessageHandler，不启真实网关）**

```csharp
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.AI;

public class HttpLlmGatewayTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private static HttpLlmGateway CreateGateway(HttpMessageHandler handler, string token = "")
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddHttpClient(nameof(HttpLlmGateway))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .ConfigureHttpClient(c => c.BaseAddress = new System.Uri("http://gateway.test/"));
        var factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var options = Options.Create(new AiGatewayOptions { ApiToken = token });
        return new HttpLlmGateway(factory, options, NullLogger<HttpLlmGateway>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_Returns_Text_From_Gateway()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"text\":\"条款：...\",\"finishReason\":\"stop\",\"attempts\":1,\"latencySeconds\":0.5}",
                System.Text.Encoding.UTF8,
                "application/json")
        };
        var handler = new StubHandler(response);
        var gateway = CreateGateway(handler);

        var text = await gateway.CompleteAsync("system", "user");

        Assert.Equal("条款：...", text);
        Assert.Equal("http://gateway.test/v1/chat", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_Throws_With_Service_Code_On_502()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("{\"code\":\"PROVIDER_UNAVAILABLE\",\"message\":\"all down\"}")
        };
        var gateway = CreateGateway(new StubHandler(response));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => gateway.CompleteAsync("system", "user"));
        Assert.Equal(BidCompareErrorCodes.AiGatewayFailed, ex.Code);
        Assert.Equal("PROVIDER_UNAVAILABLE", ex.Data["serviceCode"]);
    }
}
```

- [ ] **Step 2：运行测试**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet test test\DredgeAI.BidCompare.Application.Tests --filter HttpLlmGatewayTests
```
Expected: `Passed!`（2 个用例）。

- [ ] **Step 3：Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat(abp): switch ILlmGateway to ai-gateway HTTP client"
```

## 9. Phase 5：用量持久化 + admin-web 接线

### Task 5.1：AiUsageRecord 实体 + DbContext + 迁移

**Files:**
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Domain/AI/AiUsageRecord.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.EntityFrameworkCore/EntityFrameworkCore/BidCompareDbContext.cs`

- [ ] **Step 1：创建实体**

```csharp
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.AI;

/// <summary>
/// LLM 调用用量记录：由 services/ai-gateway 经 POST /api/ai-gateway/usage-records 上报，
/// 供 admin-web「调用记录 / 用量分析」与后续限额/告警使用。
/// </summary>
public class AiUsageRecord : FullAuditedEntity<Guid>
{
    public string Business { get; private set; } = default!;
    public string UsedConfig { get; private set; } = default!;
    public string UsedModel { get; private set; } = default!;
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public int? TotalTokens { get; private set; }
    public string? FinishReason { get; private set; }
    public int Attempts { get; private set; }
    public double? LatencySeconds { get; private set; }
    public string? CircuitBreakerState { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorType { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? TextPreview { get; private set; }

    protected AiUsageRecord()
    {
    }

    public AiUsageRecord(
        Guid id,
        string business,
        string usedConfig,
        string usedModel,
        int? inputTokens,
        int? outputTokens,
        int? totalTokens,
        string? finishReason,
        int attempts,
        double? latencySeconds,
        string? circuitBreakerState,
        bool success,
        string? errorType,
        string? errorMessage,
        string? textPreview) : base(id)
    {
        Business = Check.NotNullOrWhiteSpace(business, nameof(business), maxLength: 64);
        UsedConfig = Check.NotNullOrWhiteSpace(usedConfig, nameof(usedConfig), maxLength: 128);
        UsedModel = Check.NotNullOrWhiteSpace(usedModel, nameof(usedModel), maxLength: 128);
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
        FinishReason = finishReason;
        Attempts = attempts;
        LatencySeconds = latencySeconds;
        CircuitBreakerState = circuitBreakerState;
        Success = success;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
        TextPreview = textPreview;
    }
}
```

- [ ] **Step 2：DbContext 增加 DbSet 与映射**

在 `BidCompareDbContext` 的 DbSet 区域追加：
```csharp
    public DbSet<AiUsageRecord> AiUsageRecords { get; set; }
```

在 `OnModelCreating` 的 `builder.Entity<ExportJob>(...)` 之后追加：
```csharp
        builder.Entity<AiUsageRecord>(b =>
        {
            b.ToTable("BcAiUsageRecords");
            b.ConfigureByConvention();
            b.Property(x => x.Business).IsRequired().HasMaxLength(64);
            b.Property(x => x.UsedConfig).IsRequired().HasMaxLength(128);
            b.Property(x => x.UsedModel).IsRequired().HasMaxLength(128);
            b.Property(x => x.ErrorMessage).HasMaxLength(2048);
            b.Property(x => x.TextPreview).HasMaxLength(512);
            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.UsedConfig);
            b.HasIndex(x => x.Business);
            b.HasIndex(x => new { x.Success, x.CreationTime });
        });
```

并补 `using DredgeAI.BidCompare.AI;`。

- [ ] **Step 3：生成迁移**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet ef migrations add Add_AiUsageRecords -p src\DredgeAI.BidCompare.EntityFrameworkCore -s src\DredgeAI.BidCompare.DbMigrator
```
Expected: 在 `src/DredgeAI.BidCompare.EntityFrameworkCore/Migrations/` 生成 `*_Add_AiUsageRecords.cs`。

- [ ] **Step 4：应用迁移（本地库）**

Run:
```powershell
dotnet run --project src\DredgeAI.BidCompare.DbMigrator
```
Expected: `Successfully completed database migrations`。

- [ ] **Step 5：Commit**

```bash
git add backend/DredgeAI.BidCompare/src
git commit -m "feat(abp): add AiUsageRecord entity and migration"
```

### Task 5.2：用量 DTO + AppService + AutoMapper

**Files:**
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application.Contracts/AI/AiUsageRecordDtos.cs`
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/AI/AiUsageRecordAppService.cs`
- Modify: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/BidCompareApplicationAutoMapperProfile.cs`

- [ ] **Step 1：创建 DTO**

```csharp
using System;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.AI;

public class AiUsageRecordDto : FullAuditedEntityDto<Guid>
{
    public string Business { get; set; } = default!;
    public string UsedConfig { get; set; } = default!;
    public string UsedModel { get; set; } = default!;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? FinishReason { get; set; }
    public int Attempts { get; set; }
    public double? LatencySeconds { get; set; }
    public string? CircuitBreakerState { get; set; }
    public bool Success { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TextPreview { get; set; }
}

public class CreateAiUsageRecordDto
{
    public string Business { get; set; } = "general";
    public string UsedConfig { get; set; } = "";
    public string UsedModel { get; set; } = "";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? FinishReason { get; set; }
    public int Attempts { get; set; } = 1;
    public double? LatencySeconds { get; set; }
    public string? CircuitBreakerState { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TextPreview { get; set; }
}

public class GetAiUsageRecordsInput : PagedAndSortedResultRequestDto
{
    public string? Business { get; set; }
    public string? Model { get; set; }
    public bool? Success { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class AiUsageStatsDto
{
    public long TotalCalls { get; set; }
    public long TotalTokens { get; set; }
}

public class UsageSeriesItemDto
{
    public string Name { get; set; } = default!;
    public List<int> Data { get; set; } = new();
}

public class UsageTimeSeriesDto
{
    public List<string> Categories { get; set; } = new();
    public List<UsageSeriesItemDto> ByModel { get; set; } = new();
    public List<UsageSeriesItemDto> ByKey { get; set; } = new();
    public List<UsageSeriesItemDto> ByName { get; set; } = new();
}
```

- [ ] **Step 2：创建 AppService（`[RemoteService(false)]`，由显式 Controller 暴露）**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.ObjectMapping;

namespace DredgeAI.BidCompare.AI;

[RemoteService(false)]
public class AiUsageRecordAppService : ApplicationService, IAiUsageRecordAppService
{
    private readonly IRepository<AiUsageRecord, Guid> _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IObjectMapper _objectMapper;

    public AiUsageRecordAppService(
        IRepository<AiUsageRecord, Guid> repository,
        IGuidGenerator guidGenerator,
        IObjectMapper objectMapper)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _objectMapper = objectMapper;
    }

    public async Task<AiUsageRecordDto> CreateAsync(CreateAiUsageRecordDto input)
    {
        var entity = new AiUsageRecord(
            _guidGenerator.Create(),
            input.Business,
            input.UsedConfig,
            input.UsedModel,
            input.InputTokens,
            input.OutputTokens,
            input.TotalTokens,
            input.FinishReason,
            input.Attempts,
            input.LatencySeconds,
            input.CircuitBreakerState,
            input.Success,
            input.ErrorType,
            input.ErrorMessage,
            input.TextPreview);
        await _repository.InsertAsync(entity, autoSave: true);
        return _objectMapper.Map<AiUsageRecord, AiUsageRecordDto>(entity);
    }

    public async Task<PagedResultDto<AiUsageRecordDto>> GetListAsync(GetAiUsageRecordsInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!string.IsNullOrWhiteSpace(input.Business), x => x.Business == input.Business)
            .WhereIf(!string.IsNullOrWhiteSpace(input.Model), x => x.UsedConfig == input.Model || x.UsedModel == input.Model)
            .WhereIf(input.Success.HasValue, x => x.Success == input.Success.Value)
            .WhereIf(input.StartDate.HasValue, x => x.CreationTime >= input.StartDate.Value)
            .WhereIf(input.EndDate.HasValue, x => x.CreationTime <= input.EndDate.Value);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var items = await AsyncExecuter.ToListAsync(
            queryable.OrderByDescending(x => x.CreationTime)
                .PageBy(input.SkipCount, input.MaxResultCount));
        return new PagedResultDto<AiUsageRecordDto>(
            totalCount,
            _objectMapper.Map<List<AiUsageRecord>, List<AiUsageRecordDto>>(items));
    }

    public async Task<AiUsageStatsDto> GetStatsAsync()
    {
        var queryable = await _repository.GetQueryableAsync();
        var items = await AsyncExecuter.ToListAsync(queryable);
        return new AiUsageStatsDto
        {
            TotalCalls = items.Count,
            TotalTokens = items.Sum(x => x.TotalTokens ?? 0)
        };
    }

    public async Task<UsageTimeSeriesDto> GetTimeSeriesAsync(
        string range,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var (from, to) = ResolveRange(range, startDate, endDate);
        var queryable = await _repository.GetQueryableAsync();
        var items = await AsyncExecuter.ToListAsync(
            queryable.Where(x => x.CreationTime >= from && x.CreationTime < to));

        var categories = new List<string>();
        for (var day = from.Date; day < to.Date; day = day.AddDays(1))
        {
            categories.Add(day.ToString("M/d"));
        }

        return new UsageTimeSeriesDto
        {
            Categories = categories,
            ByModel = Series(items, x => x.UsedModel, from, categories.Count),
            ByKey = Series(items, x => x.UsedConfig, from, categories.Count),
            ByName = Series(items, x => x.Business, from, categories.Count),
        };
    }

    private static (DateTime From, DateTime To) ResolveRange(
        string range,
        DateTime? startDate,
        DateTime? endDate)
    {
        var now = DateTime.UtcNow;
        return range switch
        {
            "30d" => (now.AddDays(-29).Date, now.AddDays(1).Date),
            "this-month" => (new DateTime(now.Year, now.Month, 1), now.AddDays(1).Date),
            "last-month" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1),
                new DateTime(now.Year, now.Month, 1)),
            "custom" when startDate.HasValue && endDate.HasValue =>
                (startDate.Value.Date, endDate.Value.Date.AddDays(1)),
            _ => (now.AddDays(-6).Date, now.AddDays(1).Date),
        };
    }

    private static List<UsageSeriesItemDto> Series(
        List<AiUsageRecord> items,
        Func<AiUsageRecord, string> groupBy,
        DateTime from,
        int days)
    {
        return items
            .GroupBy(groupBy)
            .Select(g =>
            {
                var data = new int[days];
                foreach (var item in g)
                {
                    var idx = (item.CreationTime.Date - from.Date).Days;
                    if (idx >= 0 && idx < days)
                    {
                        data[idx] += 1;
                    }
                }
                return new UsageSeriesItemDto { Name = g.Key, Data = data.ToList() };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }
}
```

同时在 Application.Contracts 创建接口 `IAiUsageRecordAppService.cs`：
```csharp
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.AI;

public interface IAiUsageRecordAppService
{
    Task<AiUsageRecordDto> CreateAsync(CreateAiUsageRecordDto input);
    Task<PagedResultDto<AiUsageRecordDto>> GetListAsync(GetAiUsageRecordsInput input);
    Task<AiUsageStatsDto> GetStatsAsync();
    Task<UsageTimeSeriesDto> GetTimeSeriesAsync(string range, DateTime? startDate = null, DateTime? endDate = null);
}
```

> `WhereIf` 扩展位于 `Volo.Abp.Linq`；如编译提示缺失，补 `using Volo.Abp;`（ABP 全局命名空间自带）。`AsyncExecuter` 来自 `Volo.Abp.Application.Services.ApplicationService` 基类。

- [ ] **Step 3：AutoMapper 注册**

在 `BidCompareApplicationAutoMapperProfile` 构造器追加：
```csharp
        CreateMap<AI.AiUsageRecord, AI.AiUsageRecordDto>();
```

- [ ] **Step 4：编译验证**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet build src\DredgeAI.BidCompare.Application -v q
```
Expected: `Build succeeded`。

### Task 5.3：Controller（ingest + admin/user apikey 端点）

**Files:**
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/Controllers/AiGatewayController.cs`
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi/Controllers/ApiKeyController.cs`

> `AiGatewayOptions` 位于 HttpApi.Host，而 HttpApi 工程不引用 HttpApi.Host，因此 ingest 控制器必须放 Host 工程（与 `StorageFileController.cs` 同级）。

- [ ] **Step 1：创建 `AiGatewayController.cs`（ingest）**

```csharp
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Route("api/ai-gateway")]
public class AiGatewayController : AbpControllerBase
{
    private readonly IAiUsageRecordAppService _usageAppService;
    private readonly AiGatewayOptions _options;

    public AiGatewayController(
        IAiUsageRecordAppService usageAppService,
        IOptions<AiGatewayOptions> options)
    {
        _usageAppService = usageAppService;
        _options = options.Value;
    }

    /// <summary>POST /api/ai-gateway/usage-records 网关用量上报（X-Gateway-Token 校验）。</summary>
    [HttpPost("usage-records")]
    public async Task<AiUsageRecordDto> CreateUsageRecordAsync([FromBody] CreateAiUsageRecordDto input)
    {
        if (!string.IsNullOrWhiteSpace(_options.IngestToken)
            && Request.Headers["X-Gateway-Token"] != _options.IngestToken)
        {
            throw new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
                .WithData("reason", "无效的网关上报令牌");
        }
        return await _usageAppService.CreateAsync(input);
    }
}
```

- [ ] **Step 2：创建 `ApiKeyController.cs`**

```csharp
using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Route("api/apikey")]
[Route("api/admin/apikey")]
public class ApiKeyController : AbpControllerBase
{
    private readonly IAiUsageRecordAppService _usageAppService;

    public ApiKeyController(IAiUsageRecordAppService usageAppService)
    {
        _usageAppService = usageAppService;
    }

    /// <summary>GET /api/*/apikey/usage-stats 用量汇总。</summary>
    [HttpGet("usage-stats")]
    public Task<AiUsageStatsDto> GetUsageStatsAsync()
        => _usageAppService.GetStatsAsync();

    /// <summary>GET /api/*/apikey/usage-timeseries 用量时序（range=7d|30d|this-month|last-month|custom）。</summary>
    [HttpGet("usage-timeseries")]
    public Task<UsageTimeSeriesDto> GetUsageTimeSeriesAsync(
        [FromQuery] string range,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
        => _usageAppService.GetTimeSeriesAsync(range, startDate, endDate);

    /// <summary>GET /api/*/apikey/records 调用记录（分页）。</summary>
    [HttpGet("records")]
    public Task<PagedResultDto<AiUsageRecordDto>> GetUsageRecordsAsync(
        [FromQuery] GetAiUsageRecordsInput input)
        => _usageAppService.GetListAsync(input);
}
```

- [ ] **Step 3：编译验证**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet build src\DredgeAI.BidCompare.HttpApi -v q
```
Expected: `Build succeeded`。

### Task 5.4：用量 AppService 测试

**Files:**
- Create: `backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests/AI/AiUsageRecordAppServiceTests.cs`

- [ ] **Step 1：创建测试（复用 SQLite 测试底座）**

```csharp
using System;
using System.Threading.Tasks;
using Xunit;

namespace DredgeAI.BidCompare.AI;

public class AiUsageRecordAppServiceTests : BidCompareApplicationTestBase
{
    [Fact]
    public async Task Create_And_List_Usage_Records()
    {
        var appService = GetRequiredService<IAiUsageRecordAppService>();

        await appService.CreateAsync(new CreateAiUsageRecordDto
        {
            Business = "bid-compare",
            UsedConfig = "Qwen3.6-A3B",
            UsedModel = "Qwen3.6-35B-A3B-FP8",
            InputTokens = 100,
            OutputTokens = 50,
            TotalTokens = 150,
            FinishReason = "stop",
            Attempts = 1,
            LatencySeconds = 0.5,
            CircuitBreakerState = "closed",
            Success = true
        });
        await appService.CreateAsync(new CreateAiUsageRecordDto
        {
            Business = "standard-qa",
            UsedConfig = "Qwen3.6-A3B",
            UsedModel = "Qwen3.6-35B-A3B-FP8",
            Success = false,
            ErrorType = "PROVIDER_UNAVAILABLE",
            ErrorMessage = "all down"
        });

        var list = await appService.GetListAsync(new GetAiUsageRecordsInput
        {
            MaxResultCount = 10
        });
        Assert.Equal(2, list.TotalCount);

        var filtered = await appService.GetListAsync(new GetAiUsageRecordsInput
        {
            Business = "bid-compare",
            MaxResultCount = 10
        });
        Assert.Equal(1, filtered.TotalCount);

        var stats = await appService.GetStatsAsync();
        Assert.Equal(2, stats.TotalCalls);
        Assert.Equal(150, stats.TotalTokens);

        var series = await appService.GetTimeSeriesAsync("7d");
        Assert.Equal(7, series.Categories.Count);
        Assert.Contains(series.ByModel, x => x.Name == "Qwen3.6-35B-A3B-FP8");
    }
}
```

> 基类为 `BidCompareApplicationTestBase`（继承 `BidCompareTestBase<BidCompareApplicationTestModule>`，已有 SQLite 建表）。若该基类名不同，以 `test/.../BidCompareApplicationTestBase.cs` 实际类名为准。

- [ ] **Step 2：运行测试**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet test test\DredgeAI.BidCompare.Application.Tests --filter AiUsageRecordAppServiceTests
```
Expected: `Passed!`（1 个用例）。

- [ ] **Step 3：Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat(abp): persist usage records and expose stats endpoints"
```

### Task 5.5：admin-web 调用记录/用量接真

**Files:**
- Modify: `packages/shared/src/core/api/urls.ts`
- Modify: `packages/shared/src/core/api/modules/apikey.ts`
- Modify: `admin-web/src/api/modules/apikey.ts`
- Modify: `admin-web/src/views/api/index.vue`

- [ ] **Step 1：共享 URL 与 API 增加 records**

`urls.ts` 追加：
```ts
  apiKeyRecords: '/apikey/records',
```

`packages/shared/src/core/api/modules/apikey.ts` 追加：
```ts
    getUsageRecords: (params?: Record<string, string>): Promise<{ items: ApiUsageRecord[], totalCount: number }> =>
      request.get<{ items: ApiUsageRecord[], totalCount: number }>(urls.apiKeyRecords, { params }),
```

`packages/shared/src/core/types/apikey.ts` 追加：
```ts
export interface ApiUsageRecord {
  id: string
  business: string
  usedConfig: string
  usedModel: string
  inputTokens?: number
  outputTokens?: number
  totalTokens?: number
  finishReason?: string
  attempts: number
  latencySeconds?: number
  circuitBreakerState?: string
  success: boolean
  errorType?: string
  errorMessage?: string
  creationTime: string
}
```

`admin-web/src/api/modules/apikey.ts` 导出追加：
```ts
  getUsageRecords,
```

- [ ] **Step 2：`admin-web/src/views/api/index.vue` 调用记录改走接口**

新增状态与加载：
```ts
const callRecords = ref<CallRecord[]>([])
const callRecordsLoading = ref(false)

async function fetchCallRecords(): Promise<void> {
  callRecordsLoading.value = true
  try {
    const page = await getUsageRecords({ MaxResultCount: '200' })
    callRecords.value = page.items.map((r) => ({
      id: r.id,
      userName: r.business,
      department: r.usedConfig,
      modelName: r.usedModel,
      inputTokens: r.inputTokens ?? 0,
      outputTokens: r.outputTokens ?? 0,
      userPhone: '',
      latency: r.latencySeconds ? Math.round(r.latencySeconds * 1000) : 0,
      status: r.success ? '成功' : '失败',
      time: r.creationTime.slice(0, 19).replace('T', ' '),
    }))
  } catch {
    message.error('加载调用记录失败')
  } finally {
    callRecordsLoading.value = false
  }
}
```

替换原 `const callRecords = computed(...)` 中的 mock 源：
1. 删除 `mockCallRecords` 生成块与 `callRecords` 的 mock computed；
2. `callRecords` 改为 `ref<CallRecord[]>([])`；
3. 新增客户端筛选 computed（复用原有三个筛选 ref）：

```ts
const filteredCallRecords = computed(() => {
  let list = callRecords.value
  const kw = callUserKeyword.value.trim()
  if (kw) list = list.filter((r) => r.userName.includes(kw))
  if (callModelFilter.value.length > 0 && callModelFilter.value.length < allModelNames.length) {
    list = list.filter((r) => callModelFilter.value.includes(r.modelName))
  }
  if (callStatusFilter.value) {
    list = list.filter((r) => r.status === callStatusFilter.value)
  }
  return list
})
```

4. `CallsTab` 的 `:records` 由 `callRecords` 改为 `filteredCallRecords`，并传 `:loading="callRecordsLoading"`（如 CallsTab 尚无 `loading` prop，在 `admin-web/src/views/api/components/CallsTab.vue` 增加该 prop 并透传给表格 `:loading`）；
5. 在 `onMounted` 中 `fetchCallRecords()`。

- [ ] **Step 3：类型检查**

Run:
```powershell
pnpm run typecheck
```
Expected: 双端 `vue-tsc --noEmit` 通过。

- [ ] **Step 4：Commit**

```bash
git add packages/shared admin-web/src
git commit -m "feat(admin-web): wire call records and usage to real ABP endpoints"
```

## 10. Phase 6：流式代理 + 前端聊天基础设施

### Task 6.1：ABP SSE 代理端点

**Files:**
- Create: `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/Controllers/AiGatewayChatController.cs`

> 该端点复用 `HttpLlmGateway` 命名 HttpClient（在 Host 模块注册）与 `OwnedStream`（Host 工程），因此放 Host 工程（与 `StorageFileController.cs` 同级）。

- [ ] **Step 1：创建 `AiGatewayChatController.cs`（SSE 透传）**

```csharp
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>POST /api/ai-gateway/chat/stream：前端统一问答端点，SSE 透传 services/ai-gateway。</summary>
[Route("api/ai-gateway")]
public class AiGatewayChatController : AbpControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public AiGatewayChatController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("chat/stream")]
    public async Task<System.IO.Stream> ChatStreamAsync([FromBody] ChatStreamRequest input)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLlmGateway));
        var upstream = await client.PostAsJsonAsync(
            "v1/chat/stream",
            input,
            JsonOptions,
            HttpContext.RequestAborted);
        upstream.EnsureSuccessStatusCode();

        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        var stream = await upstream.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
        return new OwnedStream(stream, upstream);
    }
}

public class ChatStreamRequest
{
    public List<ChatStreamMessage> Messages { get; set; } = new();
    public string? Mode { get; set; }
    public string? ConfigName { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string? Business { get; set; }
}

public class ChatStreamMessage
{
    public string Role { get; set; } = default!;
    public JsonElement? Content { get; set; }
}
```

> `OwnedStream` 位于 `DredgeAI.BidCompare` 命名空间（`src/.../HttpApi.Host/OwnedStream.cs`），Host 工程内可直接引用。

- [ ] **Step 2：编译验证**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet build src\DredgeAI.BidCompare.HttpApi -v q
```
Expected: `Build succeeded`。

- [ ] **Step 3：Commit**

```bash
git add backend/DredgeAI.BidCompare/src
git commit -m "feat(abp): add SSE chat stream proxy endpoint"
```

### Task 6.2：共享聊天类型

**Files:**
- Modify: `packages/shared/src/core/types/chat.ts`

- [ ] **Step 1：替换 `chat.ts`**

```ts
export interface ChatMessage {
  role: 'system' | 'user' | 'assistant'
  content: string
}

export interface ChatRequest {
  messages: ChatMessage[]
  mode?: 'instruct' | 'thinking'
  configName?: string
  temperature?: number
  maxTokens?: number
  business?: string
}

export interface ChatUsage {
  prompt_tokens?: number
  completion_tokens?: number
  total_tokens?: number
}

export interface ChatResult {
  text: string
  finishReason: string | null
  usage: ChatUsage | null
  usedConfig: string | null
  usedModel: string | null
  attempts: number
  latencySeconds: number | null
  circuitBreakerState: string | null
}

export interface ChatDoneEvent extends ChatResult {
  type: 'done'
}

export interface ChatDeltaEvent {
  type: 'delta'
  text: string
}

export interface ChatStreamFailedEvent {
  type: 'stream_failed'
  text: string
  error: { type: string, message: string }
  usedConfig: string | null
  usedModel: string | null
  attempts: number
  latencySeconds: number | null
  circuitBreakerState: string | null
}

export interface ChatErrorEvent {
  type: 'error'
  error: { type: string, message: string }
}

export type ChatStreamEvent =
  | ChatDeltaEvent
  | ChatDoneEvent
  | ChatStreamFailedEvent
  | ChatErrorEvent
```

- [ ] **Step 2：Commit**

```bash
git add packages/shared/src/core/types/chat.ts
git commit -m "feat(shared): extend chat types for streaming transport"
```

### Task 6.3：AIChatTransport + useAIChat + AIChat 流式

**Files:**
- Create: `packages/shared/src/web/chat/transport.ts`
- Create: `packages/shared/src/web/composables/useAIChat.ts`
- Modify: `packages/shared/src/web/components/AIChat.vue`
- Modify: `packages/shared/src/web/index.ts`

- [ ] **Step 1：创建 `transport.ts`**

```ts
import type { ChatDoneEvent, ChatRequest, ChatResult } from '@shared/core/types/chat'

export interface ChatStreamHandlers {
  onDelta: (text: string) => void
  onDone: (event: ChatDoneEvent) => void
  onFailed: (text: string, error: { type: string, message: string }) => void
  onError: (error: { type: string, message: string }) => void
}

export interface ChatTransport {
  chat: (req: ChatRequest) => Promise<ChatResult>
  chatStream: (req: ChatRequest, handlers: ChatStreamHandlers, signal?: AbortSignal) => Promise<void>
}

function parseEvent(line: string): ChatDoneEvent | { type: string, [k: string]: unknown } | null {
  const trimmed = line.trim()
  if (!trimmed.startsWith('data: ')) return null
  return JSON.parse(trimmed.slice(6))
}

export function createChatTransport(baseUrl = '/api/ai-gateway/chat/stream'): ChatTransport {
  return {
    async chat(req: ChatRequest): Promise<ChatResult> {
      const response = await fetch(baseUrl.replace('/chat/stream', '/chat'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      })
      if (!response.ok) throw new Error(`chat failed: ${response.status}`)
      return await response.json() as ChatResult
    },

    async chatStream(req, handlers, signal) {
      const response = await fetch(baseUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
        signal,
      })
      if (!response.ok || !response.body) {
        handlers.onError({ type: 'HTTP_ERROR', message: `stream failed: ${response.status}` })
        return
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''
      for (;;) {
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''
        for (const line of lines) {
          const event = parseEvent(line)
          if (!event) continue
          switch (event.type) {
            case 'delta':
              handlers.onDelta(String(event.text ?? ''))
              break
            case 'done':
              handlers.onDone(event as ChatDoneEvent)
              break
            case 'stream_failed':
              handlers.onFailed(String(event.text ?? ''), event.error as { type: string, message: string })
              return
            case 'error':
              handlers.onError(event.error as { type: string, message: string })
              return
          }
        }
      }
    },
  }
}
```

- [ ] **Step 2：创建 `useAIChat.ts`**

```ts
import { ref } from 'vue'
import type { ChatMessage } from '@shared/core/types/chat'
import type { ChatTransport } from '@shared/web/chat/transport'

export function useAIChat(transport: ChatTransport, initial: ChatMessage[] = []) {
  const messages = ref<ChatMessage[]>([...initial])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const streamingText = ref('')

  async function send(text: string): Promise<void> {
    const question = text.trim()
    if (!question || loading.value) return

    messages.value.push({ role: 'user', content: question })
    loading.value = true
    error.value = null
    streamingText.value = ''
    try {
      await transport.chatStream(
        {
          messages: [...messages.value],
          mode: 'instruct',
          business: 'standard-qa',
        },
        {
          onDelta: (delta) => { streamingText.value += delta },
          onDone: (event) => {
            messages.value.push({ role: 'assistant', content: event.text })
            streamingText.value = ''
          },
          onFailed: (text, err) => {
            if (text) messages.value.push({ role: 'assistant', content: text })
            error.value = err.message
          },
          onError: (err) => { error.value = err.message },
        },
      )
    } catch (e) {
      error.value = e instanceof Error ? e.message : '对话失败'
    } finally {
      loading.value = false
      streamingText.value = ''
    }
  }

  return { messages, loading, error, streamingText, send }
}
```

- [ ] **Step 3：`AIChat.vue` 支持流式文本与错误**

新增 props：
```ts
  streamingText?: string
  error?: string | null
```

模板：在消息列表末尾、`loading` 时渲染流式气泡：
```vue
    <div v-if="loading || streamingText" class="chat-msg chat-msg--assistant">
      <div class="chat-avatar">AI</div>
      <div class="chat-bubble">
        {{ streamingText }}<span v-if="loading" class="chat-cursor">▍</span>
      </div>
    </div>
    <div v-if="error" class="chat-error">{{ error }}</div>
```

样式追加：
```less
.chat-cursor { animation: chat-blink 1s step-end infinite; }
@keyframes chat-blink { 50% { opacity: 0; } }
.chat-error { padding: 0 @spacing-md @spacing-sm; color: @danger; font-size: @font-size-sm; }
@media (prefers-reduced-motion: reduce) {
  .chat-cursor { animation: none; }
}
```

- [ ] **Step 4：`packages/shared/src/web/index.ts` 追加导出**

```ts
export { createChatTransport } from './chat/transport'
export type { ChatTransport, ChatStreamHandlers } from './chat/transport'
export { useAIChat } from './composables/useAIChat'
```

- [ ] **Step 5：类型检查**

Run:
```powershell
pnpm run typecheck
```
Expected: 双端通过。

- [ ] **Step 6：Commit**

```bash
git add packages/shared/src
git commit -m "feat(shared): add SSE chat transport, useAIChat and streaming AIChat"
```

### Task 6.4：transport 测试

**Files:**
- Create: `user-web/__tests__/ai-chat-transport.test.ts`

- [ ] **Step 1：创建测试**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createChatTransport } from '@shared/web/chat/transport'

function sseResponse(...chunks: string[]): Response {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(new TextEncoder().encode(chunk))
      controller.close()
    },
  })
  return new Response(body, { status: 200 })
}

afterEach(() => vi.unstubAllGlobals())

describe('createChatTransport.chatStream', () => {
  it('parses delta and done events', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(
      'data: {"type":"delta","text":"你"}\n\n',
      'data: {"type":"delta","text":"好"}\n\n',
      'data: {"type":"done","text":"你好","finishReason":"stop","attempts":1,"usedConfig":"fake","usedModel":"m","latencySeconds":0.1,"usage":{"total_tokens":5},"circuitBreakerState":"closed"}\n\n',
    )))

    const deltas: string[] = []
    let doneText = ''
    const transport = createChatTransport('/api/ai-gateway/chat/stream')
    await transport.chatStream(
      { messages: [{ role: 'user', content: 'hi' }] },
      {
        onDelta: (t) => deltas.push(t),
        onDone: (e) => { doneText = e.text },
        onFailed: () => {},
        onError: () => {},
      },
    )

    expect(deltas).toEqual(['你', '好'])
    expect(doneText).toBe('你好')
  })

  it('stops on stream_failed and keeps partial text', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(
      'data: {"type":"delta","text":"部分"}\n\n',
      'data: {"type":"stream_failed","text":"部分","error":{"type":"LLMStreamError","message":"中断"}}\n\n',
    )))

    let partial = ''
    let failed = false
    const transport = createChatTransport('/api/ai-gateway/chat/stream')
    await transport.chatStream(
      { messages: [{ role: 'user', content: 'hi' }] },
      {
        onDelta: (t) => { partial += t },
        onDone: () => {},
        onFailed: (text) => { failed = text === '部分' },
        onError: () => {},
      },
    )

    expect(partial).toBe('部分')
    expect(failed).toBe(true)
  })
})
```

- [ ] **Step 2：运行测试**

Run:
```powershell
pnpm --filter user-web test
```
Expected: `2 passed`（原有 constants/compare-labels 测试仍通过）。

- [ ] **Step 3：Commit**

```bash
git add user-web/__tests__
git commit -m "test(user-web): cover SSE chat transport parsing"
```

### Task 6.5（可选）：StandardProperty 接通流式问答

**Files:**
- Modify: `user-web/src/views/standards/components/StandardProperty.vue`

- [ ] **Step 1：环境变量开关 + 真实 transport**

```ts
import { createChatTransport } from '@shared/web/chat/transport'
import { useAIChat } from '@shared/web/composables/useAIChat'

const chatEnabled = import.meta.env.VITE_AI_CHAT_ENABLED === 'true'
const chat = useAIChat(createChatTransport(), [{ role: 'assistant', content: '你好！可以针对所选标准向我提问。' }])

function handleChat(text: string): void {
  if (!chatEnabled) {
    chatMessages.value.push({ role: 'user', content: text })
    setTimeout(() => {
      chatMessages.value.push({ role: 'ai', content: '已收到您的问题。请查阅规范原文以获取最准确的信息。' })
    }, 600)
    return
  }
  void chat.send(text)
}
```

`chatEnabled` 为真时，模板的 `AIChat` 绑定改为 `:messages="chat.messages" :loading="chat.loading" :streaming-text="chat.streamingText" :error="chat.error"`。

- [ ] **Step 2：类型检查**

Run:
```powershell
pnpm --filter user-web typecheck
```
Expected: 通过。

- [ ] **Step 3：Commit**

```bash
git add user-web/src/views/standards/components/StandardProperty.vue
git commit -m "feat(user-web): wire standard AI chat behind VITE_AI_CHAT_ENABLED flag"
```

## 11. Phase 7：部署与收尾

### Task 7.1：start.ps1 接入 ai-gateway

**Files:**
- Modify: `start.ps1`

- [ ] **Step 1：端口/路径/清理/启动/健康检查/汇总**

在端口约定区追加：
```powershell
$aiGatewayPort = 8200
$aiGatewayUrl = "http://localhost:$aiGatewayPort"
$aiGatewayDir = Join-Path $rootDir "services\ai-gateway"
$aiGatewayPython = Join-Path $aiGatewayDir ".venv\Scripts\python.exe"
$aiGatewayLogPath = Join-Path $logsDir "ai-gateway.log"
$aiGatewayPidPath = Join-Path $logsDir "ai-gateway.pid"
```

前置检查（compare-algo venv 检查之后）：
```powershell
if (-not (Test-Path $aiGatewayPython)) {
    Write-Error "ai-gateway venv not found: $aiGatewayPython"; exit 1
}
Write-Host "  ai-gateway venv OK" -ForegroundColor DarkGray
```

清理区追加：
```powershell
Stop-PortProcess -Label "ai-gateway" -Port $aiGatewayPort
```

启动区追加：
```powershell
$escapedAiGatewayDir = $aiGatewayDir.Replace("'", "''")
$escapedAiGatewayPython = $aiGatewayPython.Replace("'", "''")
$aiGatewayCommand = "Set-Location '$escapedAiGatewayDir'; & '$escapedAiGatewayPython' -m uvicorn app.main:app --host 127.0.0.1 --port $aiGatewayPort"
$aiGatewayProcess = Start-ServiceProcess -ServiceName "ai-gateway" -ServiceCommand $aiGatewayCommand -LogPath $aiGatewayLogPath -PidPath $aiGatewayPidPath
```

健康检查区追加：
```powershell
$aiGatewayHealthy = Test-HttpHealth -Label "ai-gateway" -Url "$aiGatewayUrl/healthz" -TimeoutSeconds 60
```

汇总区追加 `ai-gateway` 行，并把 `$aiGatewayLogPath` 加入 `Watch-ServiceLogs` 参数列表与 PID 打印。

- [ ] **Step 2：端到端启动验证**

Run:
```powershell
.\start.ps1 -NoBrowser
```
Expected: 汇总区显示 `ai-gateway OK`，`http://localhost:8200/healthz` 返回 200。

- [ ] **Step 3：Commit**

```bash
git add start.ps1
git commit -m "chore(dev): start ai-gateway in startup script"
```

### Task 7.2：README / .env / 安全文档

**Files:**
- Modify: `README.md`
- Modify: `docs/security/key-rotation.md`

- [ ] **Step 1：README 更新**

- 服务表新增一行：`ai-gateway | http://localhost:8200 | AI 推理网关（多模型路由/重试/熔断/SSE）`；
- 配置节新增：
  - `AI_GATEWAY_BASE_URL`、`AI_GATEWAY_API_TOKEN`（ABP→网关）、`AI_GATEWAY_INGEST_TOKEN`（网关→ABP）；
  - `LLM_CONFIGS`（JSON 数组，示例含 Qwen3.6 配置）与 `ANGINEER_*` 超时/重试/熔断参数说明；
- 比标模块流程中「LLM 条款响应判定与关键指标比选」一句补充「经 ai-gateway 调用」；
- 已知限制更新：LLM 未配置时 AI 分析自动降级不变；新增「ai-gateway 未启动时 ABP 的 LLM 调用返回 AiGatewayFailed，AI 分析降级为暂不可用」。

- [ ] **Step 2：`docs/security/key-rotation.md` 更新**

在密钥清单表（`AnGIneer / LLM API Key` 行附近）补充：

| 密钥 | 存处 | 轮换方式 |
|---|---|---|
| `AI_GATEWAY_API_TOKEN` / `AI_GATEWAY_INGEST_TOKEN` | 仅 `.env` | 修改后重启 ai-gateway 与后端 |

并注明：网关与后端之间的令牌不属于用户数据，禁止入库、禁止进日志（日志仅打印「已配置」）。

- [ ] **Step 3：Commit**

```bash
git add README.md docs/security/key-rotation.md
git commit -m "docs: document ai-gateway env vars and token rotation"
```

### Task 7.3：全量回归

- [ ] **Step 1：Python 测试**

Run:
```powershell
cd services\ai-gateway
uv run pytest -q
cd ..\compare-algo
uv run pytest -q
```
Expected: ai-gateway `13 passed`；compare-algo 原有用例全部通过。

- [ ] **Step 2：.NET 测试**

Run:
```powershell
cd backend\DredgeAI.BidCompare
dotnet test test\DredgeAI.BidCompare.Application.Tests
dotnet test test\DredgeAI.BidCompare.Domain.Tests
dotnet test test\DredgeAI.BidCompare.EntityFrameworkCore.Tests
```
Expected: 全部 `Passed!`（含新增 HttpLlmGateway / AiUsageRecordAppService 用例）。

- [ ] **Step 3：前端类型检查 + 测试**

Run:
```powershell
pnpm run typecheck
pnpm --filter user-web test
```
Expected: 双端 typecheck 通过；user-web 测试通过。

- [ ] **Step 4：手工链路验证**

1. 启动全套（`.\start.ps1 -NoBrowser`）；
2. `curl http://localhost:8200/v1/models` 返回模型列表（api_key 脱敏）；
3. Swagger 中触发一次 `POST /api/compare/tasks/{id}/clauses/extract`，确认返回条款或 `AiGatewayFailed`（未配 LLM 时）；
4. `GET /api/admin/apikey/usage-records` 能看到网关上报的调用记录；
5. 前端 admin 页「调用记录」「用量分析」展示真实数据。

## 12. 已知边界与后续（本计划不实现）

- **模型管理 / 权限控制 / 告警管理**仍为 mock：模型 CRUD 需网关配置管理接口，用户限额需 ABP 用户链路，均属后续计划。
- **用量时序「用户维度」**暂用 `business` 作为维度名，ABP 用户身份接入后替换为真实用户。
- **网关高可用**：熔断器为进程内状态，多副本部署时状态不共享；后续可选 Redis 外置（库的 P1 建议）。
- **调用方直接消费库**（非 HTTP 网关）不在本计划范围：`ai-inference` 仍可被 AnGIneer 侧直接 import，DredgeAI 一律经网关。
