import request from '@/api/request'
import type { LineChartData, PieChartData } from '@/types'

export function getDailyApiCalls(): Promise<LineChartData> {
  return request.get('/analytics/daily-api-calls')
}

export function getModelUsage(): Promise<PieChartData> {
  return request.get('/analytics/model-usage')
}

export function getUserGrowth(): Promise<LineChartData> {
  return request.get('/analytics/user-growth')
}

export function getErrorRate(): Promise<LineChartData> {
  return request.get('/analytics/error-rate')
}
