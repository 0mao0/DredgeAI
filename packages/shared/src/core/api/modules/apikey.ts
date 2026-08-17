import { urls } from '@shared/core/api'
import type { RequestInstance } from '@shared/core/http/types'
import type { ApiKey, ApiUsageRecord, ModelType, UsageByModel, UsageByKey, ApiUsageStats, UsageTimeSeries } from '@shared/core/types'

export function createApikeyApi(request: RequestInstance) {
  return {
    getApiKeyList: (): Promise<ApiKey[]> =>
      request.get<ApiKey[]>(urls.apiKeyList),

    getModelTypes: (): Promise<ModelType[]> =>
      request.get<ModelType[]>(urls.apiKeyModels),

    getUsageByModel: (): Promise<UsageByModel[]> =>
      request.get<UsageByModel[]>(urls.apiKeyUsageByModel),

    getUsageByKey: (): Promise<UsageByKey[]> =>
      request.get<UsageByKey[]>(urls.apiKeyUsageByKey),

    getUsageStats: (): Promise<ApiUsageStats> =>
      request.get<ApiUsageStats>(urls.apiKeyUsageStats),

    getUsageTimeSeries: (range: string, extra?: Record<string, string>): Promise<UsageTimeSeries> =>
      request.get<UsageTimeSeries>(urls.apiKeyUsageTimeSeries, { params: { range, ...extra } }),

    getUsageRecords: (params?: Record<string, string>): Promise<{ items: ApiUsageRecord[], totalCount: number }> =>
      request.get<{ items: ApiUsageRecord[], totalCount: number }>(urls.apiKeyRecords, { params }),
  }
}
