import { urls } from '@shared/core/api'
import request from '@/api/request'
import { API_BASE_URL } from '@/utils/constants'
import type {
  ClauseItem,
  CompareDocMeta,
  ComparePair,
  CompareTask,
  CompareTaskStatus,
  ConfirmClausesPayload,
  EvidenceItem,
  RiskLevel,
  SimilarityMatrix,
  TaskOverview,
} from '@/types'

function fillUrl(template: string, params: Record<string, string>): string {
  return Object.entries(params).reduce((url, [key, value]) => url.replace(`:${key}`, value), template)
}

function silentConfig(silent: boolean): { headers: { 'X-Silent-Request': '1' } } | undefined {
  return silent ? { headers: { 'X-Silent-Request': '1' } } : undefined
}

/* —— 后端 DTO（ABP 契约：枚举为 int，字段 camelCase） —— */

interface CompareTaskDto {
  id: string
  name: string
  nameEditedByUser: boolean
  suggestedName?: string | null
  status: number
  failureReason?: string | null
  docIds: string[]
  tenderDocId?: string | null
  clauseSnapshot?: ClauseDto[] | null
  progress: {
    stage: string
    percent: number
    message?: string | null
    pairIndex?: number | null
    pairCount?: number | null
  }
  pairs?: ComparePairDto[] | null
  createdAt: string
}

interface ComparePairDto {
  pairId: string
  docAId: string
  docBId: string
  status: number
  similarity?: number | null
  failReason?: string | null
  startedAt?: string | null
  finishedAt?: string | null
}

interface CompareDocumentDto {
  id: string
  taskId: string
  role: number
  fileName: string
  fileSize: number
  parseStatus: number
  parseError?: string | null
  pageCount?: number | null
  ocrLowConfidenceRatio?: number | null
  createdAt: string
}

interface EvidenceLocationDto {
  docId: string
  blockIds: string[]
}

interface EvidenceDto {
  id: string
  taskId: string
  type: number
  severity: number
  docIds: string[]
  locations: EvidenceLocationDto[]
  title: string
  description: string
  metrics?: Record<string, unknown> | null
  aiGenerated: boolean
}

interface ClauseDto {
  clauseId: string
  source: number
  text: string
  mandatory: boolean
  category?: string | null
}

interface ClauseTemplateDto {
  id: string
  text: string
  mandatory: boolean
  category?: string | null
}

interface ExportJobDto {
  jobId: string
  taskId: string
  format: number
  status: number
  downloadUrl?: string | null
  error?: string | null
}

interface IrBlockDto {
  blockId: string
  pageIdx: number
  bbox: number[]
  text: string
}

interface DocumentIrDto {
  docId: string
  pages: unknown[]
  blocks: IrBlockDto[]
}

/* —— 枚举映射（后端按 ABP 标准输出 int） —— */

const TASK_STATUS_MAP: Record<number, CompareTaskStatus> = {
  0: 'parsing', // Parsing
  1: 'parsing', // Parsed（尚未进入比对，进度由 stage 区分）
  2: 'parsing', // AwaitingClauses
  3: 'comparing', // Comparing
  4: 'ai_analyzing', // Analyzing
  5: 'completed', // Done
  6: 'failed', // Failed
  7: 'partial', // Partial
}

const PARSE_STATUS_MAP: Record<number, CompareDocMeta['parseStatus']> = {
  0: 'pending', // Pending
  1: 'parsing', // Parsing
  2: 'done', // Parsed
  3: 'failed', // Failed
}

const EVIDENCE_TYPE_MAP: Record<number, EvidenceItem['type']> = {
  0: 'similarity', // Similarity
  1: 'price', // Pricing
  2: 'metadata', // Metadata
  3: 'clause', // Clause
  4: 'indicator', // Indicator
}

const SEVERITY_MAP: Record<number, RiskLevel> = {
  0: 'high', // High
  1: 'mid', // Mid
  2: 'low', // Low
}

const EXPORT_STATUS_MAP: Record<number, 'processing' | 'done' | 'failed'> = {
  0: 'processing', // Pending
  1: 'processing', // Running
  2: 'done', // Succeeded
  3: 'failed', // Failed
}

const PAIR_STATUS_MAP: Record<number, ComparePair['status']> = {
  0: 'waiting', // Waiting
  1: 'processing', // Processing
  2: 'done', // Done
  3: 'failed', // Failed
}

/* —— DTO → 视图模型 —— */

function mapTask(dto: CompareTaskDto, documents: CompareDocMeta[]): CompareTask {
  const stage = dto.progress?.stage ?? 'parsing'
  const percent = dto.progress?.percent ?? 0
  const progress = stage === 'comparing'
    ? { parse: 100, compare: percent, ai: 0 }
    : stage === 'analyzing'
      ? { parse: 100, compare: 100, ai: percent }
      : { parse: percent, compare: 0, ai: 0 }

  return {
    id: dto.id,
    name: dto.name,
    nameEditedByUser: dto.nameEditedByUser ?? false,
    suggestedName: dto.suggestedName ?? null,
    status: TASK_STATUS_MAP[dto.status] ?? 'parsing',
    failReason: dto.failureReason ?? undefined,
    documents,
    tenderDocId: dto.tenderDocId ?? null,
    progress: {
      stage,
      ...progress,
      pairIndex: dto.progress?.pairIndex ?? undefined,
      pairCount: dto.progress?.pairCount ?? undefined,
      message: dto.progress?.message ?? undefined,
    },
    pairs: dto.pairs?.map(mapPair) ?? undefined,
    clauseSnapshot: dto.clauseSnapshot?.map((c) => mapClause(c, 'ai_extracted')) ?? null,
    createdAt: dto.createdAt,
  }
}

function mapPair(dto: ComparePairDto): ComparePair {
  return {
    pairId: dto.pairId,
    docAId: dto.docAId,
    docBId: dto.docBId,
    status: PAIR_STATUS_MAP[dto.status] ?? 'waiting',
    similarity: dto.similarity ?? undefined,
    failReason: dto.failReason ?? undefined,
    startedAt: dto.startedAt ?? undefined,
    finishedAt: dto.finishedAt ?? undefined,
  }
}

function mapDocument(dto: CompareDocumentDto): CompareDocMeta {
  return {
    id: dto.id,
    taskId: dto.taskId,
    fileName: dto.fileName,
    pages: dto.pageCount ?? 0,
    sizeBytes: dto.fileSize,
    parseStatus: PARSE_STATUS_MAP[dto.parseStatus] ?? 'pending',
    role: dto.role === 1 ? 'tender' : 'bid',
    failReason: dto.parseError ?? undefined,
    isLowConfidenceOcr: dto.ocrLowConfidenceRatio != null && dto.ocrLowConfidenceRatio > 0.3,
  }
}

/** 生成文档原文预览 URL（PDF Viewer 经 fetch/pdf.js 直接请求，不走 axios 实例）。 */
export function getDocumentFileUrl(taskId: string, docId: string): string {
  const path = fillUrl(urls.compareTaskDocumentFile, { id: taskId, docId }).replace(/^\//, '')
  return `${API_BASE_URL}${path}`
}

function mapClause(dto: ClauseDto, source: ClauseItem['source']): ClauseItem {
  return {
    id: dto.clauseId,
    title: dto.text,
    content: dto.text,
    category: dto.category ?? '',
    mandatory: dto.mandatory,
    source,
  }
}

function mapTemplate(dto: ClauseTemplateDto): ClauseItem {
  return {
    id: dto.id,
    title: dto.text,
    content: dto.text,
    category: dto.category ?? '',
    mandatory: dto.mandatory,
    source: 'library',
  }
}

function mapExportJob(dto: ExportJobDto): { exportId: string, status: 'processing' | 'done' | 'failed', downloadUrl?: string } {
  return {
    exportId: dto.jobId,
    status: EXPORT_STATUS_MAP[dto.status] ?? 'processing',
    downloadUrl: dto.downloadUrl ?? undefined,
  }
}

/* —— 任务 —— */

export async function getTasks(): Promise<CompareTask[]> {
  const res = await request.get<{ items: CompareTaskDto[], totalCount: number }>(urls.compareTasks, {
    params: { MaxResultCount: 10 },
  })
  return Promise.all(res.items.map(async (t) => mapTask(t, await getDocuments(t.id))))
}

export async function createTask(name: string): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(urls.compareTasks, { name })
  return mapTask(dto, [])
}

export async function startParse(id: string, silent = false): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(
    fillUrl(urls.compareTaskStartParse, { id }),
    undefined,
    silentConfig(silent),
  )
  const documents = await getDocuments(id, silent)
  return mapTask(dto, documents)
}

export async function reparseTask(id: string, docIds?: string[], silent = false): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(
    fillUrl(urls.compareTaskReparse, { id }),
    docIds?.length ? { docIds } : undefined,
    silentConfig(silent),
  )
  const documents = await getDocuments(id, silent)
  return mapTask(dto, documents)
}

export async function retryCompare(id: string, pairIds?: string[], silent = false): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(
    fillUrl(urls.compareTaskCompareRetry, { id }),
    pairIds?.length ? { pairIds } : undefined,
    silentConfig(silent),
  )
  const documents = await getDocuments(id, silent)
  return mapTask(dto, documents)
}

export async function updateTaskName(id: string, name: string, silent = false): Promise<CompareTask> {
  const dto = await request.put<CompareTaskDto>(
    fillUrl(urls.compareTaskName, { id }),
    { name },
    silentConfig(silent),
  )
  const documents = await getDocuments(id, silent)
  return mapTask(dto, documents)
}

export async function getTask(id: string, silent = false): Promise<CompareTask> {
  const [dto, documents] = await Promise.all([
    request.get<CompareTaskDto>(fillUrl(urls.compareTask, { id }), silentConfig(silent)),
    getDocuments(id, silent),
  ])
  return mapTask(dto, documents)
}

export async function getDocuments(id: string, silent = false): Promise<CompareDocMeta[]> {
  const res = await request.get<CompareDocumentDto[]>(fillUrl(urls.compareTaskDocuments, { id }), silentConfig(silent))
  return res.map(mapDocument)
}

export async function uploadDocument(
  taskId: string,
  file: File,
  role: 'bid' | 'tender',
  onProgress?: (percent: number) => void,
): Promise<CompareDocMeta> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('role', role === 'tender' ? '1' : '0')
  const dto = await request.post<CompareDocumentDto>(fillUrl(urls.compareTaskDocuments, { id: taskId }), formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000,
    onUploadProgress: (e) => {
      if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100))
    },
  })
  return mapDocument(dto)
}

/* —— 证据 / 矩阵 —— */

async function getIr(taskId: string, docId: string, silent = false): Promise<DocumentIrDto | null> {
  try {
    return await request.get<DocumentIrDto>(fillUrl(urls.compareTaskIr, { id: taskId, docId }), silentConfig(silent))
  } catch {
    return null
  }
}

async function buildRefs(taskId: string, ev: EvidenceDto, silent = false): Promise<EvidenceItem['refs']> {
  const refs: EvidenceItem['refs'] = []
  for (const loc of ev.locations) {
    const ir = await getIr(taskId, loc.docId, silent)
    if (!ir) continue
    for (const blockId of loc.blockIds) {
      const block = ir.blocks.find((b) => b.blockId === blockId)
      if (block && block.bbox.length === 4) {
        refs.push({
          docId: loc.docId,
          page: block.pageIdx + 1,
          bbox: [block.bbox[0], block.bbox[1], block.bbox[2], block.bbox[3]],
          pairId: ev.id,
          excerpt: block.text,
        })
      }
    }
  }
  return refs
}

export async function getEvidence(id: string, silent = false): Promise<EvidenceItem[]> {
  const res = await request.get<{ items: EvidenceDto[] }>(fillUrl(urls.compareTaskEvidences, { id }), {
    ...silentConfig(silent),
    params: { MaxResultCount: 100 },
  })
  const items: EvidenceItem[] = []
  for (const ev of res.items) {
    items.push({
      id: ev.id,
      taskId: ev.taskId,
      type: EVIDENCE_TYPE_MAP[ev.type] ?? 'metadata',
      severity: SEVERITY_MAP[ev.severity] ?? 'low',
      docIds: ev.docIds,
      title: ev.title,
      summary: ev.description,
      detail: ev.description,
      metrics: ev.metrics ?? undefined,
      refs: await buildRefs(id, ev, silent),
      source: ev.aiGenerated ? 'ai' : 'algo',
      status: 'final',
    })
  }
  return items
}

export async function getMatrix(id: string, silent = false): Promise<SimilarityMatrix> {
  return request.get<SimilarityMatrix>(fillUrl(urls.compareTaskMatrix, { id }), silentConfig(silent))
}

export async function getOverview(id: string): Promise<TaskOverview> {
  const [matrix, evidence] = await Promise.all([getMatrix(id), getEvidence(id)])
  const docLabels = matrix.docIds.map((_, i) => String.fromCharCode(65 + i))
  const simMatrix = matrix.docIds.map((a) =>
    matrix.docIds.map((b) => matrix.cells.find((c) => c.docAId === a && c.docBId === b)?.similarity ?? 0))
  const pairs = matrix.cells
    .filter((c) => c.docAId !== c.docBId)
    .map((c) => ({
      docA: c.docAId,
      docB: c.docBId,
      overall: c.similarity,
      textSim: c.similarity,
      structureSim: c.similarity,
      priceSim: c.similarity,
    }))
  return { docLabels, simMatrix, simMatrixSelf: simMatrix, pairs, evidence }
}

/* —— 条款 —— */

export async function extractClauses(taskId: string): Promise<ClauseItem[]> {
  const res = await request.post<ClauseDto[]>(fillUrl(urls.compareTaskClauseExtract, { id: taskId }))
  return res.map((c) => mapClause(c, 'ai_extracted'))
}

export async function confirmClauses(payload: ConfirmClausesPayload): Promise<CompareTask> {
  const dto = await request.put<CompareTaskDto>(fillUrl(urls.compareTaskClauses, { id: payload.taskId }), {
    clauses: payload.clauses.map((c) => ({
      clauseId: c.clauseId,
      text: c.content || c.title,
      mandatory: c.mandatory,
    })),
  })
  const documents = await getDocuments(payload.taskId)
  return mapTask(dto, documents)
}

export async function getClauseLibrary(): Promise<ClauseItem[]> {
  const res = await request.get<{ items: ClauseTemplateDto[] }>(urls.compareClauseTemplates, {
    params: { MaxResultCount: 100 },
  })
  return res.items.map(mapTemplate)
}

export async function createClause(payload: Omit<ClauseItem, 'id'>): Promise<ClauseItem> {
  const dto = await request.post<ClauseTemplateDto>(urls.compareClauseTemplates, {
    text: payload.content || payload.title,
    mandatory: payload.mandatory,
    category: payload.category || undefined,
  })
  return mapTemplate(dto)
}

export async function updateClause(id: string, payload: Partial<ClauseItem>): Promise<ClauseItem> {
  const dto = await request.put<ClauseTemplateDto>(fillUrl(urls.compareClauseTemplate, { id }), {
    text: payload.content || payload.title,
    mandatory: payload.mandatory,
    category: payload.category || undefined,
  })
  return mapTemplate(dto)
}

export async function deleteClause(id: string): Promise<void> {
  await request.delete<void>(fillUrl(urls.compareClauseTemplate, { id }))
}

/* —— 导出 —— */

export interface ExportJob {
  exportId: string
  status: 'processing' | 'done' | 'failed'
  downloadUrl?: string
}

export async function exportReport(taskId: string, format: 'docx' | 'pdf'): Promise<ExportJob> {
  const dto = await request.post<ExportJobDto>(fillUrl(urls.compareTaskExport, { id: taskId }), {
    format: format === 'pdf' ? 0 : 1,
  })
  return mapExportJob(dto)
}

export async function getExportStatus(taskId: string, exportId: string): Promise<ExportJob> {
  const dto = await request.get<ExportJobDto>(fillUrl(urls.compareTaskExportStatus, { id: taskId, exportId }))
  return mapExportJob(dto)
}
