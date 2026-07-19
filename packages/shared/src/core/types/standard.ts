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
