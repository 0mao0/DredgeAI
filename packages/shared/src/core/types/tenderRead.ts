export type TenderReadingTaskStatus
  = | 'uploading'
    | 'parsing'
    | 'parsed'
    | 'extracting'
    | 'reviewing'
    | 'ready'
    | 'partial'
    | 'failed'

export type BaselineFieldStatus = 'auto' | 'needs_review' | 'confirmed' | 'edited'

export type BaselineCategory
  = | 'project_info'
    | 'rejection_clauses'
    | 'evaluation_criteria'
    | 'technical_parameters'
    | 'commercial_data'
    | 'chapter_outline'
    | 'seal_rules'
    | 'dark_bid_format_rules'

export type TenderReadingParseStatus = 'pending' | 'parsing' | 'parsed' | 'failed'

export interface TenderReadingTask {
  id: string
  name: string
  projectCode?: string | null
  status: TenderReadingTaskStatus
  progressStage: string
  progressPercent: number
  failureReason?: string | null
  docIds: string[]
  createdAt: string
}

export interface TenderReadingDocument {
  id: string
  taskId: string
  fileName: string
  fileSize: number
  parseStatus: TenderReadingParseStatus
  parseError?: string | null
  parseProgress?: number | null
  parseStage?: string | null
  parseStageMessage?: string | null
  parseStartedAt?: string | null
  parseFinishedAt?: string | null
  pageCount?: number | null
  createdAt: string
}

export interface SourceRef {
  fieldId: string
  blockId: string
  pageIdx: number
  bbox: number[]
  text: string
}

export interface BaselineField {
  id: string
  taskId: string
  category: BaselineCategory
  fieldKey: string
  /** 后端返回结构化值 JSON 字符串，前端按需 JSON.parse */
  valueJson: string
  rawText: string
  confidence: number
  status: BaselineFieldStatus
  extractor: string
  extractorVersion: string
  sourceRefs: SourceRef[]
}

export interface TenderReadingBaseline {
  taskId: string
  fields: BaselineField[]
}

export interface TenderReadingOutlineNode {
  title: string
  level: number
  blockId?: string | null
  children: TenderReadingOutlineNode[]
}

/** 读标解析产物（后端 /document/parsed 返回） */
export interface TenderReadingParsedDocument {
  content: string
  ir: TenderReadingIr
}

export interface TenderReadingIr {
  schemaVersion: string
  docId: string
  meta: Record<string, unknown>
  pages: TenderReadingIrPage[]
  outline: TenderReadingOutlineNode[]
  blocks: TenderReadingIrBlock[]
}

export interface TenderReadingIrPage {
  pageIdx: number
  width: number
  height: number
}

export interface TenderReadingIrBlock {
  blockId: string
  pageIdx: number
  /** 0~1 归一化坐标 [x0,y0,x1,y1] */
  bbox: number[]
  type: string
  text: string
  textLevel: number
  source?: string | null
  confidence?: number | null
  table?: { html: string, imgPath: string } | null
  imgPath?: string | null
}
