import { urls, fillUrl } from '@shared/core/api'
import { overviewDocLabels } from '@shared/core/utils/compare'
import request from '@/api/request'
import { API_BASE_URL } from '@/utils/constants'
import type {
  BlockRange,
  ClauseItem,
  CompareDocMeta,
  CompareDraftDocument,
  ComparePair,
  CompareTask,
  CompareTaskStatus,
  ConfirmClausesPayload,
  EvidenceItem,
  SimilarityMatrix,
  TaskOverview,
} from '@/types'

function silentConfig(silent: boolean): { headers: { 'X-Silent-Request': '1' } } | undefined {
  return silent ? { headers: { 'X-Silent-Request': '1' } } : undefined
}

/* —— 后端 DTO（ABP 契约：枚举序列化为 snake_case 字符串，字段 camelCase） —— */

/** 后端任务状态（经 TASK_STATUS_MAP 折叠为前端展示态） */
type BackendTaskStatus = 'parsing' | 'parsed' | 'awaiting_clauses' | 'comparing' | 'analyzing' | 'done' | 'failed' | 'partial'
type BackendPairStatus = 'waiting' | 'processing' | 'done' | 'failed'
type BackendParseStatus = 'pending' | 'parsing' | 'parsed' | 'failed'
type BackendDocRole = 'bid' | 'tender'
type BackendEvidenceType = 'similarity' | 'pricing' | 'metadata' | 'clause' | 'indicator'
type BackendSeverity = 'high' | 'mid' | 'low'
type BackendClauseSource = 'extracted' | 'manual' | 'template'
type BackendExportFormat = 'pdf' | 'word'
type BackendExportStatus = 'pending' | 'running' | 'succeeded' | 'failed'

interface CompareTaskDto {
  id: string
  name: string
  nameEditedByUser: boolean
  suggestedName?: string | null
  status: BackendTaskStatus
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
  status: BackendPairStatus
  similarity?: number | null
  failReason?: string | null
  startedAt?: string | null
  finishedAt?: string | null
}

interface CompareDocumentDto {
  id: string
  taskId: string
  role: BackendDocRole
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
  ocrLowConfidenceRatio?: number | null
  createdAt: string
}

interface CompareDraftDocumentDto {
  id: string
  draftId: string
  role: BackendDocRole
  fileName: string
  fileSize: number
  createdAt: string
}

interface EvidenceLocationDto {
  docId: string
  blockIds: string[]
}

interface EvidenceDto {
  id: string
  taskId: string
  type: BackendEvidenceType
  severity: BackendSeverity
  docIds: string[]
  locations: EvidenceLocationDto[]
  title: string
  description: string
  metrics?: Record<string, unknown> | null
  aiGenerated: boolean
}

interface ClauseDto {
  clauseId: string
  source: BackendClauseSource
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
  format: BackendExportFormat
  status: BackendExportStatus
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

/* —— 枚举映射（后端输出 snake_case 字符串，此处折叠为前端展示语义） —— */

/** 后端 8 态折叠为前端 7 态：Parsed / AwaitingClauses 均展示为 parsing（进度细节由 stage 区分） */
const TASK_STATUS_MAP: Record<BackendTaskStatus, CompareTaskStatus> = {
  parsing: 'parsing',
  parsed: 'parsing',
  awaiting_clauses: 'parsing',
  comparing: 'comparing',
  analyzing: 'ai_analyzing',
  done: 'completed',
  failed: 'failed',
  partial: 'partial',
}

const PARSE_STATUS_MAP: Record<BackendParseStatus, CompareDocMeta['parseStatus']> = {
  pending: 'pending',
  parsing: 'parsing',
  parsed: 'done',
  failed: 'failed',
}

const EVIDENCE_TYPE_MAP: Record<BackendEvidenceType, EvidenceItem['type']> = {
  similarity: 'similarity',
  pricing: 'price',
  metadata: 'metadata',
  clause: 'clause',
  indicator: 'indicator',
}

const EXPORT_STATUS_MAP: Record<BackendExportStatus, 'processing' | 'done' | 'failed'> = {
  pending: 'processing',
  running: 'processing',
  succeeded: 'done',
  failed: 'failed',
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
    status: TASK_STATUS_MAP[dto.status],
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
    status: dto.status,
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
    parseStatus: PARSE_STATUS_MAP[dto.parseStatus],
    parseProgress: dto.parseProgress ?? undefined,
    parseStage: dto.parseStage ?? undefined,
    parseStageMessage: dto.parseStageMessage ?? undefined,
    parseStartedAt: dto.parseStartedAt ?? undefined,
    parseFinishedAt: dto.parseFinishedAt ?? undefined,
    role: dto.role,
    failReason: dto.parseError ?? undefined,
    isLowConfidenceOcr: dto.ocrLowConfidenceRatio != null && dto.ocrLowConfidenceRatio > 0.3,
  }
}

function mapDraftDocument(dto: CompareDraftDocumentDto): CompareDraftDocument {
  return {
    id: dto.id,
    draftId: dto.draftId,
    role: dto.role,
    fileName: dto.fileName,
    fileSize: dto.fileSize,
    createdAt: dto.createdAt,
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

export async function createTask(name: string, draftId?: string): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(
    urls.compareTasks,
    draftId ? { name, draftId } : { name },
  )
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

export async function retryAiAnalysis(id: string, silent = false): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(
    fillUrl(urls.compareTaskAiRetry, { id }),
    undefined,
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
  formData.append('role', role)
  const dto = await request.post<CompareDocumentDto>(fillUrl(urls.compareTaskDocuments, { id: taskId }), formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000,
    onUploadProgress: (e) => {
      if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100))
    },
  })
  return mapDocument(dto)
}

/* —— 上传会话（选中即上传，仅暂存文件，不建任务） —— */

export async function uploadDraftDocument(
  draftId: string,
  file: File,
  role: 'bid' | 'tender',
  onProgress?: (percent: number) => void,
): Promise<CompareDraftDocument> {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('role', role)
  const dto = await request.post<CompareDraftDocumentDto>(
    fillUrl(urls.compareDraftDocuments, { draftId }),
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 120000,
      onUploadProgress: (e) => {
        if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100))
      },
    },
  )
  return mapDraftDocument(dto)
}

export async function getDraftDocuments(draftId: string): Promise<CompareDraftDocument[]> {
  const res = await request.get<CompareDraftDocumentDto[]>(fillUrl(urls.compareDraft, { draftId }))
  return res.map(mapDraftDocument)
}

export async function deleteDraftDocument(draftId: string, docId: string): Promise<void> {
  await request.delete<void>(fillUrl(urls.compareDraftDocument, { draftId, docId }))
}

export async function deleteDraft(draftId: string): Promise<void> {
  await request.delete<void>(fillUrl(urls.compareDraft, { draftId }))
}

/* —— 证据 / 矩阵 —— */

async function getIr(
  taskId: string,
  docId: string,
  cache?: Map<string, DocumentIrDto | null>,
): Promise<DocumentIrDto | null> {
  const key = `${taskId}:${docId}`
  const hit = cache?.get(key)
  if (hit !== undefined) return hit
  try {
    const ir = await request.get<DocumentIrDto>(fillUrl(urls.compareTaskIr, { id: taskId, docId }), silentConfig(true))
    cache?.set(key, ir)
    return ir
  } catch {
    cache?.set(key, null)
    return null
  }
}

/** IR block 需要 4 元 bbox 才能定位；超大文档中表格/图片等块 bbox 可能为 null，必须容错。 */
function hasValidBbox(block: { bbox?: number[] | null }): block is { bbox: number[] } & typeof block {
  return Array.isArray(block.bbox) && block.bbox.length === 4
}

interface IrBlockLike {
  pageIdx: number
  bbox?: number[] | null
  text?: string | null
}

/** 由 IR block 生成定位 ref；bbox 缺失时退化为“仅跳页”ref（hasRect: false）。 */
function blockRef(docId: string, block: IrBlockLike, pairId: string, excerptOverride?: string | null): BlockRange {
  const valid = hasValidBbox(block)
  return {
    docId,
    page: block.pageIdx + 1,
    bbox: valid ? [block.bbox![0], block.bbox![1], block.bbox![2], block.bbox![3]] : [0, 0, 0, 0],
    hasRect: valid,
    pairId,
    excerpt: (excerptOverride ?? block.text) ?? undefined,
  }
}

async function buildRefs(
  taskId: string,
  ev: EvidenceDto,
  irCache: Map<string, DocumentIrDto | null>,
): Promise<EvidenceItem['refs']> {
  const refs: EvidenceItem['refs'] = []

  const items = ev.metrics?.items
  if (Array.isArray(items) && items.length > 0) {
    for (const item of items) {
      const blockIds = (item as { blockIds?: Record<string, string> }).blockIds
      if (!blockIds) continue
      for (const [docId, blockId] of Object.entries(blockIds)) {
        const ir = await getIr(taskId, docId, irCache)
        if (!ir) continue
        const block = ir.blocks.find((b) => b.blockId === blockId)
        if (block && Number.isInteger(block.pageIdx)) {
          const excerpt = typeof (item as { text?: unknown }).text === 'string'
            ? (item as { text: string }).text
            : block.text
          refs.push(blockRef(docId, block, `${ev.id}-${(item as { index?: number }).index ?? refs.length}`, excerpt))
        }
      }
    }
    return refs
  }

  for (const loc of ev.locations) {
    const ir = await getIr(taskId, loc.docId, irCache)
    if (!ir) continue
    for (const blockId of loc.blockIds) {
      const block = ir.blocks.find((b) => b.blockId === blockId)
      if (block && Number.isInteger(block.pageIdx)) {
        refs.push(blockRef(loc.docId, block, ev.id, block.text))
      }
    }
  }
  return refs
}

/** 按 blockId 列表取定位坐标（局部雷同片段高亮用）；bbox 缺失时退化为仅跳页。 */
export async function getBlockRefs(
  taskId: string,
  docId: string,
  blockIds: string[],
): Promise<BlockRange[]> {
  if (!blockIds.length) return []
  const ir = await getIr(taskId, docId)
  if (!ir) return []
  const refs: BlockRange[] = []
  for (const blockId of blockIds) {
    const block = ir.blocks.find((b) => b.blockId === blockId)
    if (block && Number.isInteger(block.pageIdx)) {
      refs.push(blockRef(docId, block, `${docId}-${blockId}`, block.text))
    }
  }
  return refs
}

export async function getEvidence(id: string, silent = false): Promise<EvidenceItem[]> {
  const res = await request.get<{ items: EvidenceDto[] }>(fillUrl(urls.compareTaskEvidences, { id }), {
    ...silentConfig(silent),
    params: { MaxResultCount: 100 },
  })
  // 同一次加载内同一文档 IR 只请求一次（大文件 IR 可达数千 block，串行重复拉取会明显拖慢/触发断连）
  const irCache = new Map<string, DocumentIrDto | null>()
  const items: EvidenceItem[] = []
  for (const ev of res.items) {
    const item: EvidenceItem = {
      id: ev.id,
      taskId: ev.taskId,
      type: EVIDENCE_TYPE_MAP[ev.type],
      severity: ev.severity,
      docIds: ev.docIds,
      title: ev.title,
      summary: ev.description,
      detail: ev.description,
      metrics: ev.metrics ?? undefined,
      refs: [],
      source: ev.aiGenerated ? 'ai' : 'algo',
      status: 'final',
    }
    try {
      item.refs = await buildRefs(id, ev, irCache)
    } catch {
      /* 单条证据定位失败不回退整个列表，标题/摘要仍可展示 */
    }
    items.push(item)
  }
  return items
}

export async function getMatrix(id: string, silent = false): Promise<SimilarityMatrix> {
  return request.get<SimilarityMatrix>(fillUrl(urls.compareTaskMatrix, { id }), silentConfig(silent))
}

export function assembleOverview(
  matrix: SimilarityMatrix,
  evidence: EvidenceItem[],
  documents: CompareDocMeta[],
): TaskOverview {
  const docLabels = overviewDocLabels(matrix.docIds, documents)
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

export async function getOverview(id: string): Promise<TaskOverview> {
  const [matrix, evidence, documents] = await Promise.all([getMatrix(id), getEvidence(id), getDocuments(id)])
  return assembleOverview(matrix, evidence, documents)
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
    format: format === 'pdf' ? 'pdf' : 'word',
  })
  return mapExportJob(dto)
}

export async function getExportStatus(taskId: string, exportId: string): Promise<ExportJob> {
  const dto = await request.get<ExportJobDto>(fillUrl(urls.compareTaskExportStatus, { id: taskId, exportId }))
  return mapExportJob(dto)
}
