import request from '@/api/request'
import type { AdminStats, DashboardMetric, LineChartData, PieChartData, SystemLog } from '@/types'

export function getAdminStats(): Promise<AdminStats> {
  return request.get('/dashboard/stats')
}

export function getDashboardMetrics(): Promise<DashboardMetric[]> {
  return request.get('/dashboard/metrics')
}

export function getApiCallsTrend(): Promise<LineChartData> {
  return request.get('/dashboard/api-calls-trend')
}

export function getAppDistribution(): Promise<PieChartData> {
  return request.get('/dashboard/app-distribution')
}

export function getActiveUsersTrend(): Promise<LineChartData> {
  return request.get('/dashboard/active-users-trend')
}

export function getRecentLogs(): Promise<SystemLog[]> {
  return request.get('/dashboard/recent-logs')
}
