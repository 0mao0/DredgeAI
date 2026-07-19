import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { ApiKey, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@/types'

export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get<ApiKey[]>(urls.apiKeyList)
}

export function getModelTypes(): Promise<ModelType[]> {
  return request.get<ModelType[]>(urls.apiKeyModels)
}

export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get<UsageByModel[]>(urls.apiKeyUsageByModel)
}

export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get<UsageByKey[]>(urls.apiKeyUsageByKey)
}

export function getUsageStats(): Promise<ApiUsageStats> {
  return request.get<ApiUsageStats>(urls.apiKeyUsageStats)
}

export function getUsageTimeSeries(range: string, extra?: Record<string, string>): Promise<UsageTimeSeries> {
  return request.get<UsageTimeSeries>(urls.apiKeyUsageTimeSeries, { params: { range, ...extra } })
}
