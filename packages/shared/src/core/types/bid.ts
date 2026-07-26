export interface BidReviewStep {
  title: string
  description: string
  status: 'wait' | 'process' | 'finish' | 'error'
  /** 0-100 进度值，对齐 AnGIneer parse task.progress */
  progress?: number
  /** 中间产物：解析出的关键词/术语列表（服务化阶段启用） */
  keyTerms?: { phrase: string, type: 'condition' | 'score_item' | 'disqualify' | 'requirement', pageIndex: number, count: number }[]
  /** 中间产物：图谱统计（服务化阶段启用） */
  graphStats?: { entities: number, relations: number, byLayer: { concept: number, condition: number, action: number } }
}

export interface RiskItem {
  id: string
  level: '高风险' | '中风险' | '低风险'
  content: string
  source: string
  suggestion?: string
}

export interface BidReviewSession {
  id: string
  document: string
  date: string
  riskCount: number
  status: '已完成' | '进行中'
  snippets?: { role: 'user' | 'assistant', content: string }[]
}
