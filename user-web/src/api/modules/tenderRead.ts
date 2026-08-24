import { fillUrl, urls } from '@shared/core/api'
import { API_BASE_URL } from '@/utils/constants'
import request from '@/api/request'
import type {
  BaselineCategory,
  BaselineField,
  SourceRef,
  TenderReadingBaseline,
  TenderReadingDocument,
  TenderReadingOutlineNode,
  TenderReadingParsedDocument,
  TenderReadingTask,
} from '@/types'

function silentConfig(silent: boolean): { headers: { 'X-Silent-Request': '1' } } | undefined {
  return silent ? { headers: { 'X-Silent-Request': '1' } } : undefined
}

/* —— 后端 DTO（ABP 契约：枚举 snake_case 字符串，字段 camelCase） —— */

type BackendTaskStatus = 'uploading' | 'parsing' | 'parsed' | 'extracting' | 'reviewing' | 'ready' | 'partial' | 'failed'
type BackendFieldStatus = 'auto' | 'needs_review' | 'confirmed' | 'edited'
type BackendParseStatus = 'pending' | 'parsing' | 'parsed' | 'failed'

interface TenderReadingTaskDto {
  id: string
  name: string
  projectCode?: string | null
  status: BackendTaskStatus
  progressStage: string
  progressPercent: number
  failureReason?: string | null
  docIds: string[]
  createdAt: string
}

interface TenderReadingDocumentDto {
  id: string
  taskId: string
  fileName: string
  fileSize: number
  parseStatus: BackendParseStatus
  parseError?: string | null
  parseProgress?: number | null
  parseStage?: string | null
  parseStageMessage?: string | null
  parseStartedAt?: string | null
  parseFinishedAt?: string | null
  pageCount?: number | null
  createdAt: string
}

interface SourceRefDto {
  fieldId: string
  blockId: string
  pageIdx: number
  bbox: number[]
  text: string
}

interface BaselineFieldDto {
  id: string
  taskId: string
  category: BaselineCategory
  fieldKey: string
  valueJson: string
  rawText: string
  confidence: number
  status: BackendFieldStatus
  extractor: string
  extractorVersion: string
  sourceRefs: SourceRefDto[]
}

interface TenderReadingBaselineDto {
  taskId: string
  fields: BaselineFieldDto[]
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
}

function mapTask(dto: TenderReadingTaskDto): TenderReadingTask {
  return {
    id: dto.id,
    name: dto.name,
    projectCode: dto.projectCode ?? null,
    status: dto.status,
    progressStage: dto.progressStage,
    progressPercent: dto.progressPercent,
    failureReason: dto.failureReason ?? null,
    docIds: dto.docIds,
    createdAt: dto.createdAt,
  }
}

function mapDocument(dto: TenderReadingDocumentDto): TenderReadingDocument {
  return {
    id: dto.id,
    taskId: dto.taskId,
    fileName: dto.fileName,
    fileSize: dto.fileSize,
    parseStatus: dto.parseStatus,
    parseError: dto.parseError ?? null,
    parseProgress: dto.parseProgress ?? null,
    parseStage: dto.parseStage ?? null,
    parseStageMessage: dto.parseStageMessage ?? null,
    parseStartedAt: dto.parseStartedAt ?? null,
    parseFinishedAt: dto.parseFinishedAt ?? null,
    pageCount: dto.pageCount ?? null,
    createdAt: dto.createdAt,
  }
}

function mapSourceRef(dto: SourceRefDto): SourceRef {
  return {
    fieldId: dto.fieldId,
    blockId: dto.blockId,
    pageIdx: dto.pageIdx,
    bbox: dto.bbox,
    text: dto.text,
  }
}

function mapField(dto: BaselineFieldDto): BaselineField {
  return {
    id: dto.id,
    taskId: dto.taskId,
    category: dto.category,
    fieldKey: dto.fieldKey,
    valueJson: dto.valueJson,
    rawText: dto.rawText,
    confidence: dto.confidence,
    status: dto.status,
    extractor: dto.extractor,
    extractorVersion: dto.extractorVersion,
    sourceRefs: dto.sourceRefs.map(mapSourceRef),
  }
}

function mapBaseline(dto: TenderReadingBaselineDto): TenderReadingBaseline {
  return {
    taskId: dto.taskId,
    fields: dto.fields.map(mapField),
  }
}

/** 生成读标文档原文预览 URL（PDF Viewer 直接请求，不走 axios 实例）。 */
export function getTenderReadDocumentFileUrl(taskId: string): string {
  const path = fillUrl(urls.tenderReadTaskDocumentFile, { id: taskId }).replace(/^\//, '')
  return `${API_BASE_URL}${path}`
}

/* —— 任务 —— */

export async function getTenderReadTasks(): Promise<TenderReadingTask[]> {
  const res = await request.get<PagedResult<TenderReadingTaskDto>>(urls.tenderReadTasks, {
    params: { MaxResultCount: 50 },
  })
  return res.items.map(mapTask)
}

export async function createTenderReadTask(input: { name: string, projectCode?: string }): Promise<TenderReadingTask> {
  const dto = await request.post<TenderReadingTaskDto>(urls.tenderReadTasks, input)
  return mapTask(dto)
}

export async function getTenderReadTask(id: string, silent = false): Promise<TenderReadingTask> {
  const dto = await request.get<TenderReadingTaskDto>(fillUrl(urls.tenderReadTask, { id }), silentConfig(silent))
  return mapTask(dto)
}

export async function deleteTenderReadTask(id: string): Promise<void> {
  await request.delete<void>(fillUrl(urls.tenderReadTask, { id }))
}

export async function updateTenderReadTask(
  id: string,
  input: { name: string, projectCode?: string },
): Promise<TenderReadingTask> {
  const dto = await request.put<TenderReadingTaskDto>(fillUrl(urls.tenderReadTaskName, { id }), input)
  return mapTask(dto)
}

/* —— 文档 / 解析 —— */

export async function getTenderReadDocuments(id: string, silent = false): Promise<TenderReadingDocument[]> {
  const res = await request.get<TenderReadingDocumentDto[]>(
    fillUrl(urls.tenderReadTaskDocuments, { id }),
    silentConfig(silent),
  )
  return res.map(mapDocument)
}

export async function uploadTenderReadDocument(
  taskId: string,
  file: File,
  onProgress?: (percent: number) => void,
): Promise<TenderReadingDocument> {
  const formData = new FormData()
  formData.append('file', file)
  const dto = await request.post<TenderReadingDocumentDto>(
    fillUrl(urls.tenderReadTaskDocument, { id: taskId }),
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 120000,
      onUploadProgress: (e) => {
        if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100))
      },
    },
  )
  return mapDocument(dto)
}

export async function startTenderReadParse(id: string): Promise<TenderReadingTask> {
  const dto = await request.post<TenderReadingTaskDto>(fillUrl(urls.tenderReadTaskParse, { id }))
  return mapTask(dto)
}

export async function reparseTenderReadTask(id: string): Promise<TenderReadingTask> {
  const dto = await request.post<TenderReadingTaskDto>(fillUrl(urls.tenderReadTaskReparse, { id }))
  return mapTask(dto)
}

/* —— 目录 / 基准库 / 锚点 —— */

export async function getTenderReadOutline(id: string, silent = false): Promise<TenderReadingOutlineNode[]> {
  return request.get<TenderReadingOutlineNode[]>(fillUrl(urls.tenderReadTaskOutline, { id }), silentConfig(silent))
}

export async function getTenderReadParsedDocument(id: string, silent = false): Promise<TenderReadingParsedDocument> {
  return request.get<TenderReadingParsedDocument>(
    fillUrl(urls.tenderReadTaskParsedDocument, { id }),
    silentConfig(silent),
  )
}

export async function getTenderReadBaseline(id: string, silent = false): Promise<TenderReadingBaseline> {
  const dto = await request.get<TenderReadingBaselineDto>(
    fillUrl(urls.tenderReadTaskBaseline, { id }),
    silentConfig(silent),
  )
  return mapBaseline(dto)
}

export async function exportTenderReadBaseline(id: string): Promise<TenderReadingBaseline> {
  const dto = await request.get<TenderReadingBaselineDto>(fillUrl(urls.tenderReadTaskExport, { id }))
  return mapBaseline(dto)
}

export async function updateTenderReadField(
  taskId: string,
  fieldId: string,
  input: { valueJson: string, rawText?: string, status: 'confirmed' | 'edited', confidence?: number },
): Promise<BaselineField> {
  const dto = await request.put<BaselineFieldDto>(
    fillUrl(urls.tenderReadTaskField, { id: taskId, fieldId }),
    input,
  )
  return mapField(dto)
}

/** 重抽基准库（后台任务执行）：返回进入抽取中的任务快照，前端经轮询感知完成。 */
export async function reExtractTenderReadBaseline(
  taskId: string,
  category?: BaselineCategory,
): Promise<TenderReadingTask> {
  const dto = await request.post<TenderReadingTaskDto>(
    fillUrl(urls.tenderReadTaskReExtract, { id: taskId }),
    category ? { category } : undefined,
  )
  return mapTask(dto)
}
