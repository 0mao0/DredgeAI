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

/** 用户级应用指标卡片（user-web dashboard 用） */
export interface MetricCard {
  id: string
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
  sparkline?: number[]
}
