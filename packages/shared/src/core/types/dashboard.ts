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
