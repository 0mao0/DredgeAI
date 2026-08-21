export type CompareTaskStatus
  = | 'uploading'
    | 'parsing'
    | 'comparing'
    | 'ai_analyzing'
    | 'completed'
    | 'partial'
    | 'failed'

export type ComparePairStatus = 'waiting' | 'processing' | 'done' | 'failed'

export interface ComparePair {
  pairId: string
  docAId: string
  docBId: string
  status: ComparePairStatus
  similarity?: number
  failReason?: string
  startedAt?: string
  finishedAt?: string
}

export interface CompareTask {
  id: string
  name: string
  status: CompareTaskStatus
  documents: CompareDocMeta[]
  /** 用户是否手动编辑过项目名；true 后轮询不得再自动应用 suggestedName */
  nameEditedByUser?: boolean
  /** 解析完成后后端推断的项目名建议 */
  suggestedName?: string | null
  progress: {
    /** 后端阶段：parsing / clauses / comparing / analyzing / done */
    stage?: string
    parse: number
    compare: number
    ai: number
    /** 当前比对对序号（1 起），来自后端 pairs，禁止前端估算 */
    pairIndex?: number
    pairCount?: number
    message?: string
  }
  /** 两两对比对清单（spec §8.2 逐对进度契约） */
  pairs?: ComparePair[]
  /** 条款快照（任务确认后锁定，结果页「要求」Tab 使用，禁止读全局条款库） */
  clauseSnapshot?: ClauseItem[] | null
  /** 招标文件文档 id（可选，未上传为 null） */
  tenderDocId?: string | null
  /** 来源读标任务（P3） */
  tenderReadingTaskId?: string | null
  /** 引用的读标基准库版本（P3） */
  tenderReadingBaselineVersion?: number | null
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
  /** AnGIneer 解析进度 0~100（处理中实时更新，终态 100） */
  parseProgress?: number
  /** AnGIneer 当前管线阶段（source_prep/convert/raw_parse/popo/structure/...） */
  parseStage?: string
  /** AnGIneer 当前阶段消息（如「MinerU 解析中」） */
  parseStageMessage?: string
  /** 本次解析开始时间（服务端时间戳，刷新后耗时仍可计算） */
  parseStartedAt?: string
  /** 本次解析结束时间 */
  parseFinishedAt?: string
  /** 文档原文预览 URL（由宿主 API 模块生成，PDF Viewer 直接消费） */
  fileUrl?: string
  /** 文档角色：招标文件不参与标书两两对比 */
  role?: 'bid' | 'tender'
  failReason?: string
  /** 扫描件标记：true 时 UI 提示「查重结果可能偏差」 */
  isLowConfidenceOcr?: boolean
}

/** 上传会话中的暂存文件（选中即上传，未建任务前的文件清单）。 */
export interface CompareDraftDocument {
  id: string
  draftId: string
  role: 'bid' | 'tender'
  fileName: string
  fileSize: number
  createdAt: string
}

export type RiskLevel = 'high' | 'mid' | 'low'

export type EvidenceType = 'similarity' | 'price' | 'metadata' | 'clause' | 'indicator'

export interface BlockRange {
  docId: string
  page: number
  bbox: [number, number, number, number]
  /** 是否有有效 bbox 可画高亮框；false 时仅用于跳页定位（如 IR bbox 缺失的块） */
  hasRect?: boolean
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
  /** 后端证据明细（相似度、报价规律、元数据值、条款状态、指标摘要等） */
  metrics?: Record<string, unknown>
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

export interface SimilarityCell {
  docAId: string
  docBId: string
  similarity: number
}

export interface SimilarityMatrix {
  docIds: string[]
  cells: SimilarityCell[]
}

export interface TaskOverview {
  /** 与 docLabels / simMatrix 同序的文档 id（矩阵行列顺序以 docIds 为准） */
  docIds: string[]
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
