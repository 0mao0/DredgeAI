import request from '@/api/request'
import type { ApiKey, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@/types'

export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get<ApiKey[]>('/apikey/list')
}

export function getModelTypes(): Promise<ModelType[]> {
  return request.get<ModelType[]>('/apikey/models')
}

export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get<UsageByModel[]>('/apikey/usage-by-model')
}

export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get<UsageByKey[]>('/apikey/usage-by-key')
}

export function getUsageStats(): Promise<ApiUsageStats> {
  return request.get<ApiUsageStats>('/apikey/usage-stats')
}

export function getUsageTimeSeries(range: string, extra?: Record<string, string>): Promise<UsageTimeSeries> {
  return request.get<UsageTimeSeries>('/apikey/usage-timeseries', { params: { range, ...extra } })
}
