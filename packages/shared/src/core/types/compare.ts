export type CompareTaskStatus
  = | 'uploading'
    | 'parsing'
    | 'comparing'
    | 'ai_analyzing'
    | 'completed'
    | 'partial'
    | 'failed'

export interface CompareTask {
  id: string
  name: string
  status: CompareTaskStatus
  documents: CompareDocMeta[]
  progress: { parse: number, compare: number, ai: number }
  riskSummary?: { high: number, mid: number, low: number, clauseMissing: number }
  matrixClauses?: string[]
  responseMatrix?: string[][]
  createdAt: string
  finishedAt?: string
  failReason?: string
}

export interface CompareDocMeta {
  id: string
  taskId?: string
  fileName: string
  pages: number
  sizeBytes: number
  parseStatus: 'pending' | 'parsing' | 'done' | 'failed'
  failReason?: string
  /** 扫描件标记：true 时 UI 提示「查重结果可能偏差」 */
  isLowConfidenceOcr?: boolean
}

/** 文档元数据（串标排查：作者/GUID/IP/创建工具等一致性对比） */
export interface CompareDocMetaInfo {
  docId: string
  author: string
  creatorTool: string
  producer: string
  guid: string
  ip: string
  createdAt: string
}

export type RiskLevel = 'high' | 'mid' | 'low'

export type EvidenceType = 'similarity' | 'price' | 'metadata' | 'clause'

export interface BlockRange {
  docId: string
  page: number
  bbox: [number, number, number, number]
  pairId?: string
  excerpt?: string
}

export interface EvidenceItem {
  id: string
  taskId: string
  type: EvidenceType
  severity: RiskLevel
  docIds: string[]
  title: string
  summary: string
  detail?: string
  confidence?: number
  refs: BlockRange[]
  source: 'algo' | 'ai'
  status: 'final' | 'pending'
}

export interface ClauseItem {
  id: string
  title: string
  content: string
  category: string
  mandatory: boolean
  source: 'library' | 'ai_extracted' | 'user_added'
}

export interface ConfirmClausesPayload {
  taskId: string
  clauses: { clauseId?: string, title: string, content: string, mandatory: boolean }[]
}

export interface SimilarityPair {
  docA: string
  docB: string
  overall: number
  textSim: number
  structureSim: number
  priceSim: number
}

export interface TaskOverview {
  docLabels: string[]
  simMatrix: number[][]
  simMatrixSelf: number[][]
  pairs: SimilarityPair[]
  evidence: EvidenceItem[]
}

export type IrBlockType
  = | 'para'
    | 'heading'
    | 'table'
    | 'equation'
    | 'image'
    | 'seal'
    | 'header'
    | 'footer'
    | 'page_number'
    | 'other'

export interface IrBlock {
  blockId: string
  page: number
  type: IrBlockType
  bbox: [number, number, number, number]
  text?: string
  html?: string
  latex?: string
  imagePath?: string
  source?: 'text' | 'ocr' | 'formula' | 'table' | null
  confidence?: number | null
}

export interface IrOutline {
  title: string
  page: number
  blockId: string
  level: number
}

export interface CompareIrDocument {
  schemaVersion: '2.0'
  docId: string
  pages: number
  blocks: IrBlock[]
  outline: IrOutline[]
}

export function normalizeRect(bbox: [number, number, number, number]): [number, number, number, number] {
  const [x0, y0, x1, y1] = bbox
  return [
    Math.min(x0, x1),
    Math.min(y0, y1),
    Math.max(x0, x1),
    Math.max(y0, y1),
  ]
}

export function mapIrBlocksToHighlights(
  blocks: IrBlock[],
  predicate: (b: IrBlock) => string | null,
): BlockRange[] {
  return blocks.flatMap((b) => {
    const pairId = predicate(b)
    return pairId
      ? [{ docId: '', page: b.page, bbox: normalizeRect(b.bbox), pairId, excerpt: b.text }]
      : []
  })
}
