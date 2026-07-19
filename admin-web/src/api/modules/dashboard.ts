import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { AdminStats, DashboardMetric, LineChartData, PieChartData, SystemLog } from '@/types'

export function getAdminStats(): Promise<AdminStats> {
  return request.get<AdminStats>(urls.adminStats)
}

export function getDashboardMetrics(): Promise<DashboardMetric[]> {
  return request.get<DashboardMetric[]>(urls.dashboardMetrics)
}

export function getApiCallsTrend(): Promise<LineChartData> {
  return request.get<LineChartData>(urls.dashboardApiCallsTrend)
}

export function getAppDistribution(): Promise<PieChartData> {
  return request.get<PieChartData>(urls.dashboardAppDistribution)
}

export function getActiveUsersTrend(): Promise<LineChartData> {
  return request.get<LineChartData>(urls.dashboardActiveUsersTrend)
}

export function getRecentLogs(): Promise<SystemLog[]> {
  return request.get<SystemLog[]>(urls.dashboardRecentLogs)
}
