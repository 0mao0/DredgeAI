/** 分页参数 */
export interface Pagination {
  page: number
  pageSize: number
  total: number
}

// ---------- 通用图表类型 ----------

export interface ChartSeries {
  name: string
  data: number[]
}

export interface LineChartData {
  categories: string[]
  series: ChartSeries[]
}

export interface PieChartData {
  name: string
  data: { name: string; value: number }[]
}
