import request from '@/api/request'
import { urls, fillUrl } from '@shared/core/api'
import type {
  StandardAIAnalysis,
  StandardParseBatchResult,
  StandardProperty,
  StandardPropertyInput,
} from '@/types'
import type { PagedResult } from '@shared/types'

export interface StandardQueryParams {
  keyword?: string
  industry?: string
  nature?: string
  level?: string
  status?: string
  publishYear?: number
  skipCount?: number
  maxResultCount?: number
}

export function getStandards(params?: StandardQueryParams): Promise<PagedResult<StandardProperty>> {
  return request.get<PagedResult<StandardProperty>>(urls.adminStandards, { params })
}

export function deleteStandard(id: string): Promise<void> {
  return request.delete(fillUrl(urls.adminStandardDelete, { id }))
}

export function updateStandard(id: string, data: Partial<StandardProperty>): Promise<StandardProperty> {
  return request.put<StandardProperty>(fillUrl(urls.adminStandardUpdate, { id }), data)
}

export function parseStandard(id: string): Promise<StandardAIAnalysis> {
  return request.post<StandardAIAnalysis>(fillUrl(urls.adminStandardParse, { id }))
}

/** AI 预读单文件：返回预填的元数据（当前为 mock，真实后端调用 LLM 提取） */
export function previewStandard(file: File): Promise<StandardPropertyInput> {
  const formData = new FormData()
  formData.append('file', file)
  return request.post<StandardPropertyInput>(urls.adminStandardPreview, formData, {
    timeout: 60000,
  })
}

/** 上传 PDF + 元数据，创建标准记录 */
export function uploadStandard(file: File, data: StandardPropertyInput): Promise<StandardProperty> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('metadata', JSON.stringify(data))
  return request.post<StandardProperty>(urls.adminStandardCreate, formData, {
    timeout: 120000,
  })
}

/** 批量删除，返回成功删除数量 */
export function deleteStandards(ids: string[]): Promise<number> {
  return request.post<number>(urls.adminStandardsBatchDelete, { ids })
}

/**
 * 批量解析。当前实现按 id 串行调用单条解析，逐条回调便于 UI 展示进度；
 * 真实后端提供批量端点后，可整体替换为一次 `POST /standards/batch-parse`。
 */
export async function parseStandards(
  ids: string[],
  onItem?: (result: StandardParseBatchResult) => void,
): Promise<StandardParseBatchResult[]> {
  const results: StandardParseBatchResult[] = []
  for (const id of ids) {
    try {
      const analysis = await parseStandard(id)
      const result: StandardParseBatchResult = { id, success: true, analysis }
      results.push(result)
      onItem?.(result)
    } catch (error) {
      const result: StandardParseBatchResult = {
        id,
        success: false,
        error: error instanceof Error ? error.message : '解析失败，请稍后重试',
      }
      results.push(result)
      onItem?.(result)
    }
  }
  return results
}

/** 标准原文 PDF 静态资源地址（dev/build 均由 Vite public 目录直接提供） */
export function getStandardFileUrl(id: string): string {
  return `/mock/standards/${id}.pdf`
}
