import request from '@/api/request'
import { urls, fillUrl } from '@shared/core/api'
import { API_BASE_URL } from '@/utils/constants'
import type {
  StandardResult,
  StandardSearchHistory,
  StandardCategory,
  StandardListItem,
  StandardProperty,
  StandardDocument,
  StandardAIAnalysis,
  StandardRecord,
  StandardFile,
  StandardRecordListInput,
  StandardQaRequest,
  StandardQaCitation,
} from '@/types'
import type { PagedResult } from '@shared/types'
import type {
  AIChatCitation,
  InlineCitationSearchPayload,
  QueryRequest,
  QueryResponse,
  ThinkingTraceStep,
} from '@angineer/aichat-ui'

export function getStandardResult(): Promise<StandardResult[]> {
  return request.get<StandardResult[]>(urls.standardResult)
}

export function getStandardHistory(): Promise<StandardSearchHistory[]> {
  return request.get<StandardSearchHistory[]>(urls.standardHistory)
}

export function getStandardCategories(): Promise<StandardCategory[]> {
  return request.get<StandardCategory[]>(urls.standardCategories)
}

export function getRecommendedQuestions(): Promise<string[]> {
  return request.get<string[]>(urls.standardRecommended)
}

export function getStandardList(): Promise<StandardListItem[]> {
  return request.get<StandardListItem[]>(urls.standardList)
}

export function getStandardProperty(id: string): Promise<StandardProperty> {
  return request.get<StandardProperty>(urls.standardProperty, { params: { id } })
}

export function getStandardPropertyList(): Promise<StandardProperty[]> {
  return request.get<StandardProperty[]>(urls.standardPropertyList)
}

export function getStandardDocument(id: string): Promise<StandardDocument> {
  return request.get<StandardDocument>(urls.standardDocument, { params: { id } })
}

export function getStandardAIAnalysis(id: string): Promise<StandardAIAnalysis> {
  return request.get<StandardAIAnalysis>(urls.standardAIAnalysis, { params: { id } })
}

export function updateStandardProperty(id: string, data: Partial<StandardProperty>): Promise<void> {
  return request.put(urls.standardProperty, data, { params: { id } })
}

/* —— 标准记录（数算中心同步后的本地快照，规范类型 StandardRecord） —— */

export function getStandardRecords(input?: StandardRecordListInput): Promise<PagedResult<StandardRecord>> {
  return request.get<PagedResult<StandardRecord>>(urls.standardRecords, { params: input })
}

export function getStandardRecordTree(): Promise<StandardCategory[]> {
  return request.get<StandardCategory[]>(urls.standardRecordsTree)
}

export function getStandardRecord(id: string): Promise<StandardRecord> {
  return request.get<StandardRecord>(fillUrl(urls.standardRecord, { id }))
}

export function getStandardRecordFiles(id: string): Promise<StandardFile[]> {
  return request.get<StandardFile[]>(fillUrl(urls.standardRecordFiles, { id }))
}

/** 标准附件预览 URL（PDF Viewer 经 fetch 直接请求，不走 axios 实例） */
export function getStandardRecordFileUrl(id: string, fileId: string): string {
  const path = fillUrl(urls.standardRecordFileContent, { id, fileId }).replace(/^\//, '')
  return `${API_BASE_URL}${path}`
}

export interface StandardQaAnswer {
  answer: string
  citations: StandardQaCitation[]
}

/** 规范问答（当前非流式；真实后端走 SSE，复用 chat.ts 流式事件） */
export function askStandardQa(input: StandardQaRequest): Promise<StandardQaAnswer> {
  return request.post<StandardQaAnswer>(urls.standardQaAsk, input)
}

/**
 * 与 @angineer/aichat-ui 的 AIChatTransport 结构一致（该类型未从包入口导出，
 * 这里按 api/types.ts 的约定本地声明）。
 */
export interface StandardQaTransport {
  query: (
    payload: QueryRequest,
    options?: {
      signal?: AbortSignal
      onDelta?: (delta: string) => void
      onThinking?: (steps: ThinkingTraceStep[]) => void
      onAnswerReplace?: (full: string) => void
    },
  ) => Promise<QueryResponse>
  fetchModels?: () => Promise<Array<{ name: string, configured: boolean }>>
  searchReferences?: (payload: InlineCitationSearchPayload) => Promise<{ items?: Record<string, unknown>[] }>
}

/**
 * 将标准 QA 的非流式接口适配为 @angineer/aichat-ui 的 AIChatTransport，
 * 让标准规范模块直接复用 AIChat 组件，无需自建聊天 UI。
 */
export function createStandardQaTransport(): StandardQaTransport {
  return {
    async query(payload: QueryRequest, options): Promise<QueryResponse> {
      const res = await askStandardQa({ question: payload.query })
      // 后端暂为一次性返回，先整体推送，让 AIChat 有流式输出观感
      options?.onDelta?.(res.answer)
      const citations: AIChatCitation[] = (res.citations || []).map((c) => ({
        target_id: c.standardId,
        target_type: 'standard',
        doc_id: c.standardId,
        doc_title: c.name,
        page_idx: c.page ?? 1,
        page_label: c.page ? `第 ${c.page} 页` : undefined,
        section_path: c.code ?? '',
        snippet: c.snippet,
        score: 1,
      }))
      return {
        query_id: `standard-qa-${Date.now()}`,
        intent: {
          intent_level: 'L1',
          intent_type: 'content_qa',
          parameters: {},
          required_capabilities: [],
          matched_sop: null,
          service_mode: 'semantic_retrieval',
          reason: null,
        },
        answer: res.answer,
        citations,
      }
    },
    fetchModels: async () => [],
    searchReferences: async () => ({ items: [] }),
  }
}
