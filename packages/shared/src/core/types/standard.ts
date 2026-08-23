export interface StandardResult {
  id: string
  code: string
  title: string
  match: string
  excerpt: string
  source?: string
}

export interface StandardSearchHistory {
  id: string
  query: string
  date: string
  resultCount: number
}

export interface StandardCategory {
  id: string
  name: string
  count: number
  children?: StandardCategory[]
}

export type StandardViewMode = 'table' | 'tree'

export interface StandardListItem {
  id: string
  name: string
  code: string
  parentId?: string
  children?: StandardListItem[]
}

export interface StandardProperty {
  id: string
  name: string
  code: string
  industry?: string
  nature?: string
  level?: string
  status?: string
  issuer?: string
  publishYear?: number
  parentId?: string
  uploader?: string
  description?: string
  /** 数算中心外部 id（同步来源时存在，人工补录为空） */
  externalId?: string | null
  /** 来源：remote=同步 / manual=人工补录 */
  source?: StandardSource
  /** 最近同步时间 */
  syncedAt?: string | null
  /** 子节点（树形） */
  children?: StandardProperty[]
  /** 启用状态（软屏蔽；false 表示停用，同步不重开） */
  isEnabled?: boolean
  /** 是否已完成解析（可展示原文 bbox 高亮） */
  parsed?: boolean
  /** 已解析文本块的归一化 bbox 高亮（0~1 坐标） */
  highlights?: StandardHighlight[]
}

export interface StandardHighlight {
  id: string
  itemId: string
  page: number
  left: number
  top: number
  width: number
  height: number
}

export interface StandardDocument {
  id: string
  title: string
  content: string
}

export interface StandardAIAnalysis {
  id: string
  summary: string
  keyPoints: string[]
  relatedStandards: { code: string, title: string }[]
  riskWarnings: string[]
}

export interface StandardPropertyInput {
  name: string
  code: string
  industry?: string
  nature?: string
  level?: string
  status?: string
  issuer?: string
  publishYear?: number
  parentId?: string
  uploader?: string
  description?: string
}

export interface StandardParseBatchResult {
  id: string
  success: boolean
  analysis?: StandardAIAnalysis
  error?: string
}

export type StandardSource = 'remote' | 'manual'

/** 标准记录（规范类型，对齐数算中心 StaticStandardRecordDto + 本地同步字段） */
export interface StandardRecord {
  id: string
  /** 数算中心外部 id（人工补录为 null） */
  externalId?: string | null
  /** 本地树父节点 id */
  parentId?: string | null
  /** 状态编码 */
  status?: string | null
  /** 性质编码 */
  nature?: string | null
  /** 级别编码 */
  level?: string | null
  /** 发布部门编码 */
  department?: string | null
  /** 行业编码 */
  industry?: string | null
  /** 年份 */
  year?: number
  name: string
  code?: string | null
  /** 简介 */
  content?: string | null
  isEnabled: boolean
  source: StandardSource
  /** 最近同步时间 */
  syncedAt?: string | null
  /** 数算中心 lastModificationTime（增量锚点） */
  externalUpdatedAt?: string | null
  children?: StandardRecord[]
  files?: StandardFile[]
}

/** 标准附件 */
export interface StandardFile {
  id: string
  fileName: string
  fileExtension?: string
  fileSize: number
  mimeType?: string
  parseStatus: 'pending' | 'parsing' | 'parsed' | 'failed'
  parseError?: string | null
  /** 预览下载 URL（由宿主 API 模块生成） */
  downloadUrl?: string
}

/** 标准分页/筛选查询参数 */
export interface StandardRecordListInput {
  keyword?: string
  name?: string
  code?: string
  year?: number
  level?: string
  department?: string
  industry?: string
  nature?: string
  status?: string
  parentId?: string
  skipCount?: number
  maxResultCount?: number
  sorting?: string
}

/** 规范问答请求 */
export interface StandardQaRequest {
  question: string
  topK?: number
}

/** 规范问答引用定位 */
export interface StandardQaCitation {
  standardId: string
  name: string
  code?: string
  snippet: string
  page?: number
}
