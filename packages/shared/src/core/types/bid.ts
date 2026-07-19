export interface BidReviewStep {
  title: string
  description: string
  status: 'wait' | 'process' | 'finish' | 'error'
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
  snippets?: { role: 'user' | 'assistant'; content: string }[]
}
