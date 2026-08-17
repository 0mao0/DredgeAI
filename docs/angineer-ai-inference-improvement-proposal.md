# angineer-ai-inference 改进建议（独立成库对外提供）

> 提出方：DredgeAI 团队
> 目的：希望将 `services/ai-inference` 作为**独立 Python 库**被外部项目（DredgeAI）直接依赖（不 fork、不改源码），因此需要：API 稳定、行为明确、可测试、可发布。

## 1. 背景

DredgeAI 是 .NET（ABP）+ Python 的架构：ABP 负责业务编排，Python 负责算法与 AI 调用。我们希望把“所有 LLM 对话管理”收敛到 `ai-inference`，由 DredgeAI 在上层包一层薄 HTTP 网关，`ai-inference` 保持“纯推理客户端”定位。

因此下面的改进建议只针对库本身；HTTP 服务、对外 API Key/权限/限额、用量持久化等属于消费方（网关）职责，不要求本库实现（见第 4 节）。

## 2. 当前能力（已确认）

- 多模型配置（`.env` 的 `LLM_CONFIGS`，按 priority 排序）、模型名/别名解析、多模型 fallback；
- `chat` / `chat_result` / `chat_stream` / `chat_stream_events`，OpenAI 兼容协议，支持 `tools`；
- instruct / thinking 两种模式；
- 指数退避重试（超时、断连、限流）、每模型熔断器（closed/open/half-open）；
- 输出截断守卫（`finish_reason=length` 时缩短输入重试一次，仍截断抛 `LLMTruncatedError`）；
- JSON 提取（fence 剥离、格式修复）+ Pydantic Schema 校验 + 默认值兜底；
- 日志统一、api_key 脱敏；模块级单例 `get_llm_client()`，可 `set/reset` 便于测试。

## 3. 改进建议

### P0（对外提供前必须完成）

1. **超时真正生效**
   - 现状：`TimeoutConfig` 定义了 connect/read/total，但实际只把 `total` 传给了 OpenAI SDK，connect/read 未生效。
   - 建议：使用 `httpx.Timeout(connect=..., read=..., write=..., pool=...)` 或等价方式完整传入；补测试验证三类超时分别触发。

2. **异步支持**
   - 现状：`chat`/`chat_result`/`chat_stream` 均为同步实现，高并发下会占线程。
   - 建议：提供 `async chat` / `async chat_result` / `async chat_stream`（基于 `AsyncOpenAI`），并保持同步 API 兼容；补并发测试。

3. **流式重试语义明确并实现**
   - 建议语义：拿到首个 delta **之前**失败 → 可重试/换模型；已经开始输出后失败 → 返回 partial 文本 + 失败标记（或按调用方策略丢弃重发）。
   - 需在文档和测试中固化该行为。

4. **结构化用量/元数据返回**
   - 现状：`ChatResult` 已含 usage/finish_reason/tool_calls。
   - 建议：扩展 `ChatResult`（或返回对象）增加 `latency_seconds`、`attempts`、`used_config`、`circuit_breaker_state` 等；并提供**用量回调/钩子**（由调用方决定是否持久化），本库不落库。

5. **错误分类**
   - 建议定义异常层级，例如：`ProviderUnavailableError`、`ProviderAuthError`、`RateLimitedError`、`LLMTruncatedError`、`AllProvidersFailedError`，让调用方能精确处理，而不是只拿到裸 `Exception`。

6. **依赖清理**
   - 现状：`pyproject.toml` 声明了 fastapi/uvicorn，但库内没有 server 入口。
   - 建议：要么移除这两个依赖，要么在文档中明确“本库不包含 HTTP 服务，HTTP 由消费方实现”。

7. **测试补齐**
   - 现状：仅配置解析 2 个测试。
   - 建议：覆盖 重试、熔断状态机（closed/open/half-open）、流式、截断守卫、JSON 解析器、并发/线程安全、多模型 fallback。

8. **发布与版本管理**
   - 建议：固定 `pyproject.toml` 版本号；采用语义化版本；发布到内部 PyPI 或打 git tag；提供 CHANGELOG；声明 Python 版本（>=3.10）与依赖兼容区间（如 `openai>=1.0,<2.0`）。

### P1（强烈建议）

9. **文档与示例**
   - README 覆盖：`LLM_CONFIGS` 与 `ANGINEER_*` 完整字段、instruct/thinking 模式、tools、流式、重试/熔断语义、`chat_result_guarded` 用法。

10. **熔断器可观测**
    - 现状：已有 `get_circuit_breaker_status()` / `reset_circuit_breaker()`。
    - 建议：补充成功/失败计数、最近错误信息；将“状态外部化（如 Redis）”作为可选扩展点，默认仍为进程内，避免过度设计。

11. **可选 metrics/tracing 钩子**
    - 建议：提供可选回调或 OpenTelemetry 集成点（不强制依赖），用于上报延迟、用量、熔断状态。

12. **输入校验**
    - 建议：对 `messages` / `tools` 做 Pydantic 校验，尽早暴露调用方错误。

13. **线程安全说明与测试**
    - 建议：明确单例在多线程/多 worker 下的行为，并补并发测试。

## 4. 明确不在本库范围（建议写入 README）

- HTTP 服务（FastAPI/uvicorn 入口）；
- 对外 API Key / 权限 / 限额管理；
- 调用记录与用量持久化/聚合；
- 管理后台。

以上由消费方（如 DredgeAI AI Gateway）实现；本库保持“纯推理客户端”，只负责：配置、路由、调用、可靠性、解析。

## 5. DredgeAI 的对接方式（供参考）

- 依赖方式：`ai-inference @ git+<repo>@<tag/commit>` 或内部 PyPI，钉版本；
- DredgeAI 侧包一层薄 HTTP 网关（`POST /v1/chat`、`/v1/chat/stream`、`GET /v1/models`、`/healthz`），内部 `from ai_inference import LLMClient, chat_result_guarded`；
- 网关层负责：对外 Key、限流、用量记录、告警；ABP 通过 HTTP 调用网关，业务编排不变。
