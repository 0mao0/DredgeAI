import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { LineChartData, PieChartData } from '@/types'

export function getDailyApiCalls(): Promise<LineChartData> {
  return request.get<LineChartData>(urls.analyticsDailyApiCalls)
}

export function getModelUsage(): Promise<PieChartData> {
  return request.get<PieChartData>(urls.analyticsModelUsage)
}

export function getUserGrowth(): Promise<LineChartData> {
  return request.get<LineChartData>(urls.analyticsUserGrowth)
}

export function getErrorRate(): Promise<LineChartData> {
  return request.get<LineChartData>(urls.analyticsErrorRate)
}
