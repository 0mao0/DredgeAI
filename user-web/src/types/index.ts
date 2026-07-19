export interface UserInfo {
  id: string
  name: string
  department: string
  position: string
  email: string
  phone: string
  avatar?: string
  authorizedScopes: string[]
  preferences: { theme: 'light' | 'dark'; language: 'zh-CN' | 'en-US' }
}

export interface MetricCard {
  id: string
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}

export interface AppCard {
  id: string
  title: string
  description: string
  category: '基础' | '设计' | '施工' | '经营'
  icon: string
  status: '已授权' | '待申请' | '已下架'
  route?: string
  version?: string
  pinned?: boolean
}

export interface TaskItem {
  id: string
  title: string
  status: '进行中' | '已完成' | '已暂停' | '已失败'
  updatedAt: string
  app?: string
  progress?: number
}

export interface FileItem {
  id: string
  name: string
  type: 'pdf' | 'docx' | 'xlsx' | 'pptx' | 'image' | 'other'
  size: string
  updatedAt: string
  url?: string
}

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

export interface ApiKey {
  id: string
  name: string
  key: string
  fullKey: string
  modelType: string
  createdAt: string
  status: '启用' | '禁用'
  usage: number
  quota: number
  docUrl: string
}

export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
}

export interface UsageByModel { modelName: string; calls: number; share: number }
export interface UsageByKey { keyName: string; calls: number; share: number }

export interface ChartSeries { name: string; data: number[] }
export interface LineChartData { categories: string[]; series: ChartSeries[] }
export interface PieChartData { name: string; data: { name: string; value: number }[] }

export interface ApiUsageStats {
  totalTokens: number
  totalCalls: number
}

export interface UsageTimeSeries {
  categories: string[]
  byModel: { modelName: string; data: number[] }[]
  byKey: { keyName: string; data: number[] }[]
  byName: { name: string; data: number[] }[]
}
