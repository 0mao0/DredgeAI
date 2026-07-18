import request from '@/api/request'
import type { LineChartData, PieChartData } from '@/types'

export function getDailyApiCalls(): Promise<LineChartData> {
  return request.get<LineChartData>('/analytics/daily-api-calls')
}

export function getModelUsage(): Promise<PieChartData> {
  return request.get<PieChartData>('/analytics/model-usage')
}

export function getUserGrowth(): Promise<LineChartData> {
  return request.get<LineChartData>('/analytics/user-growth')
}

export function getErrorRate(): Promise<LineChartData> {
  return request.get<LineChartData>('/analytics/error-rate')
}
