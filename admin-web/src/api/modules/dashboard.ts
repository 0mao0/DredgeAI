import request from '@/api/request'
import type { AdminStats, DashboardMetric, LineChartData, PieChartData, SystemLog } from '@/types'

export function getAdminStats(): Promise<AdminStats> {
  return request.get<AdminStats>('/dashboard/stats')
}

export function getDashboardMetrics(): Promise<DashboardMetric[]> {
  return request.get<DashboardMetric[]>('/dashboard/metrics')
}

export function getApiCallsTrend(): Promise<LineChartData> {
  return request.get<LineChartData>('/dashboard/api-calls-trend')
}

export function getAppDistribution(): Promise<PieChartData> {
  return request.get<PieChartData>('/dashboard/app-distribution')
}

export function getActiveUsersTrend(): Promise<LineChartData> {
  return request.get<LineChartData>('/dashboard/active-users-trend')
}

export function getRecentLogs(): Promise<SystemLog[]> {
  return request.get<SystemLog[]>('/dashboard/recent-logs')
}
