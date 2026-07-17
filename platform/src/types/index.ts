export interface MetricCard {
  title: string
  value: string
  trend?: string
  trendUp?: boolean
}

export interface AppCard {
  id: string
  title: string
  description: string
  category: string
  status?: string
  icon?: string
}

export interface TaskItem {
  id: string
  title: string
  status: string
  updatedAt: string
}

export interface FileItem {
  id: string
  name: string
  type: string
  updatedAt: string
}

export interface AppCategory {
  key: string
  label: string
}

export interface PermissionItem {
  id: string
  role: string
  menu: string
  app: string
  data: string
}

export interface UserInfo {
  name: string
  department: string
  position: string
  avatar?: string
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
  docUrl: string
}

export interface ModelType {
  id: string
  name: string
}

export interface KeyUsageStat {
  modelName: string
  calls: number
  share: number
}

export interface KeyUsageByKey {
  keyName: string
  calls: number
  share: number
}
