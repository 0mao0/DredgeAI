/** 分页参数 */
export interface Pagination {
  page: number
  pageSize: number
  total: number
}

/** 图表数据系列 */
export interface ChartSeries {
  name: string
  data: number[]
}

/** 折线图数据 */
export interface LineChartData {
  categories: string[]
  series: ChartSeries[]
}

/** 饼图数据 */
export interface PieChartData {
  name: string
  data: { name: string; value: number }[]
}

/** API Key 通用定义 */
export interface ApiKeyBase {
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

/** 模型类型 */
export interface ModelType {
  id: string
  name: string
  provider: string
  description?: string
}