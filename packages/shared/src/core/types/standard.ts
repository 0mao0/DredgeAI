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
  description?: string
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
  relatedStandards: { code: string; title: string }[]
  riskWarnings: string[]
}
