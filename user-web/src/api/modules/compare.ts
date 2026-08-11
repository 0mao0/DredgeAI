import { urls } from '@shared/core/api'
import request from '@/api/request'
import type {
  ClauseItem,
  CompareDocMeta,
  CompareTask,
  CompareTaskStatus,
  ConfirmClausesPayload,
  EvidenceItem,
  RiskLevel,
  TaskOverview,
} from '@/types'

function fillUrl(template: string, params: Record<string, string>): string {
  return Object.entries(params).reduce((url, [key, value]) => url.replace(`:${key}`, value), template)
}

/* —— 后端 DTO（ABP 契约：枚举为 int，字段 camelCase） —— */

interface CompareTaskDto {
  id: string
  name: string
  status: number
  docIds: string[]
  tenderDocId?: string | null
  clauseSnapshot?: unknown[] | null
  progress: { stage: string, percent: number, message?: string | null }
  createdAt: string
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
  aiGenerated: boolean
}

interface SimilarityMatrixCellDto {
  docAId: string
  docBId: string
  similarity: number
}

interface SimilarityMatrixDto {
  docIds: string[]
  cells: SimilarityMatrixCellDto[]
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
  4: 'metadata', // Indicator（前端暂无独立视图，归入 metadata）
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
    status: TASK_STATUS_MAP[dto.status] ?? 'parsing',
    documents,
    progress,
    createdAt: dto.createdAt,
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
    failReason: dto.parseError ?? undefined,
    isLowConfidenceOcr: dto.ocrLowConfidenceRatio != null && dto.ocrLowConfidenceRatio > 0.3,
  }
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
  const res = await request.get<{ items: CompareTaskDto[] }>(urls.compareTasks)
  return Promise.all(res.items.map(async (t) => mapTask(t, await getDocuments(t.id))))
}

export async function createTask(name: string): Promise<CompareTask> {
  const dto = await request.post<CompareTaskDto>(urls.compareTasks, { name })
  return mapTask(dto, [])
}

export async function getTask(id: string): Promise<CompareTask> {
  const [dto, documents] = await Promise.all([
    request.get<CompareTaskDto>(fillUrl(urls.compareTask, { id })),
    getDocuments(id),
  ])
  return mapTask(dto, documents)
}

export async function getDocuments(id: string): Promise<CompareDocMeta[]> {
  const res = await request.get<CompareDocumentDto[]>(fillUrl(urls.compareTaskDocuments, { id }))
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

async function getIr(taskId: string, docId: string): Promise<DocumentIrDto | null> {
  try {
    return await request.get<DocumentIrDto>(fillUrl(urls.compareTaskIr, { id: taskId, docId }))
  } catch {
    return null
  }
}

async function buildRefs(taskId: string, ev: EvidenceDto): Promise<EvidenceItem['refs']> {
  const refs: EvidenceItem['refs'] = []
  for (const loc of ev.locations) {
    const ir = await getIr(taskId, loc.docId)
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

export async function getEvidence(id: string): Promise<EvidenceItem[]> {
  const res = await request.get<{ items: EvidenceDto[] }>(fillUrl(urls.compareTaskEvidences, { id }))
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
      refs: await buildRefs(id, ev),
      source: ev.aiGenerated ? 'ai' : 'algo',
      status: 'final',
    })
  }
  return items
}

export async function getMatrix(id: string): Promise<SimilarityMatrixDto> {
  return request.get<SimilarityMatrixDto>(fillUrl(urls.compareTaskMatrix, { id }))
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

export async function confirmClauses(payload: ConfirmClausesPayload): Promise<void> {
  await request.put<void>(fillUrl(urls.compareTaskClauses, { id: payload.taskId }), {
    clauses: payload.clauses.map((c) => ({
      clauseId: c.clauseId,
      text: c.content || c.title,
      mandatory: c.mandatory,
    })),
  })
}

export async function getClauseLibrary(): Promise<ClauseItem[]> {
  const res = await request.get<{ items: ClauseTemplateDto[] }>(urls.compareClauseTemplates)
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
