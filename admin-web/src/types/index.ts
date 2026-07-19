export interface Pagination {
  page: number
  pageSize: number
  total: number
}

export interface AdminStats {
  totalUsers: number
  totalApps: number
  totalApiCalls: number
  activeUsers: number
  userTrend: number
  appTrend: number
  apiTrend: number
  activeUserTrend: number
}

export interface DashboardMetric {
  id: string
  title: string
  value: string | number
  suffix?: string
  trend?: number
  trendUp?: boolean
  icon?: string
  color?: string
}

export interface LineChartData {
  categories: string[]
  series: { name: string; data: number[] }[]
}

export interface PieChartData {
  name: string
  data: { name: string; value: number }[]
}

export interface ChartSeries {
  name: string
  data: number[]
}

export interface UserInfo {
  id: string
  username: string
  name: string
  email: string
  phone: string
  role: 'super_admin' | 'admin' | 'operator'
  department: string
  avatar?: string
  status: '启用' | '禁用'
  createdAt: string
  lastLogin?: string
}

export interface PermissionItem {
  id: string
  name: string
  code: string
  type: 'menu' | 'button' | 'api' | 'data'
  parentId?: string
  description?: string
  status: '启用' | '禁用'
  sort: number
}

export interface ApplicationItem {
  id: string
  name: string
  category: string
  manager: string
  version: string
  status: '运营中' | '已下架' | '开发中'
  userCount: number
  apiCalls: number
  createdAt: string
}

export interface SystemLog {
  id: string
  type: '操作日志' | '登录日志' | '系统错误' | '安全告警'
  operator: string
  content: string
  ip?: string
  createdAt: string
  level?: 'info' | 'warning' | 'error'
}

export interface DataSource {
  id: string
  name: string
  type: 'mysql' | 'postgresql' | 'api'
  status: '已连接' | '连接失败' | '未配置'
  lastSync?: string
  description?: string
}

export interface ApiKey {
  id: string
  name: string
  key: string
  fullKey: string
  modelType: string
  app: string
  status: '启用' | '禁用'
  createdAt: string
  expiredAt?: string
  lastUsed?: string
  quota: number
  usage: number
  docUrl: string
}

export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
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
