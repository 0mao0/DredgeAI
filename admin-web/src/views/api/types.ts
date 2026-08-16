export interface ModelItem {
  id: string
  name: string
  actualModel: string
  modelType: string
  ipAddress: string
  docUrl: string
  status: string
  createdAt: string
  consumption: number
}

export interface ModelForm {
  name: string
  actualModel: string
  modelType: string
  ipAddress: string
  docUrl: string
  status: string
  consumption: number
}

export interface SelectOption {
  label: string
  value: string
}

export interface ModelLimitEntry {
  modelName: string
  enabled: boolean
  callsLimit: number
  callsWarn: number
  tokensLimit: number
  tokensWarn: number
}

export interface MergedUserRecord {
  userId: string
  name: string
  department: string
  calls: number
  tokens: number
  modelLimits: ModelLimitEntry[]
}

export interface CallRecord {
  id: string
  userName: string
  userPhone: string
  department: string
  modelName: string
  inputTokens: number
  outputTokens: number
  latency: number
  status: '成功' | '失败'
  time: string
}

export interface AlertRecord {
  id: string
  userName: string
  department: string
  modelName: string
  type: 'calls' | 'tokens'
  current: number
  limit: number
  time: string
}

export interface DayjsLike { format: (tpl: string) => string }
