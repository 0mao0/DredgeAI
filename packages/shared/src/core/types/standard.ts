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
