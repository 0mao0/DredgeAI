import request from '@/api/request'
import type { ApiKey, ModelType, UsageByModel, UsageByKey } from '@/types'

export function getApiKeyList(): Promise<ApiKey[]> {
  return request.get('/apikey/list') as unknown as Promise<ApiKey[]>
}

export function getModelTypes(): Promise<ModelType[]> {
  return request.get('/apikey/models') as unknown as Promise<ModelType[]>
}

export function getUsageByModel(): Promise<UsageByModel[]> {
  return request.get('/apikey/usage-by-model') as unknown as Promise<UsageByModel[]>
}

export function getUsageByKey(): Promise<UsageByKey[]> {
  return request.get('/apikey/usage-by-key') as unknown as Promise<UsageByKey[]>
}
