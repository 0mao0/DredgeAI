export interface ApiUsageStats {
  totalTokens: number
  totalCalls: number
}

export interface UsageByModel {
  modelName: string
  calls: number
  share: number
}

export interface UsageByKey {
  keyName: string
  calls: number
  share: number
}

export interface UsageTimeSeries {
  categories: string[]
  byModel: { modelName: string; data: number[] }[]
  byKey: { keyName: string; data: number[] }[]
  byName: { name: string; data: number[] }[]
}

export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
}

// ---------- API Key（统一结构，admin 侧字段更全） ----------

export interface ApiKey {
  id: string
  name: string
  key: string
  fullKey: string
  modelType: string
  /** 该 Key 归属的应用（admin 侧用于管理"哪些人可以用哪些 API"） */
  app?: string
  status: '启用' | '禁用'
  createdAt: string
  expiredAt?: string
  lastUsed?: string
  quota: number
  usage: number
  docUrl: string
}
