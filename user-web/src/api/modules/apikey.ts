import request from '@/api/request'
import type { ApiKey, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@/types'

export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get<ApiKey[]>('/key/list')
}

export function getModelTypes(): Promise<ModelType[]> {
  return request.get<ModelType[]>('/key/models')
}

export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get<UsageByModel[]>('/key/usage-by-model')
}

export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get<UsageByKey[]>('/key/usage-by-key')
}

export function getUsageStats(): Promise<ApiUsageStats> {
  return request.get<ApiUsageStats>('/key/usage-stats')
}

export function getUsageTimeSeries(range: string, extra?: Record<string, string>): Promise<UsageTimeSeries> {
  return request.get<UsageTimeSeries>('/key/usage-timeseries', { params: { range, ...extra } })
}
