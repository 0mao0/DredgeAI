import request from '@/api/request'
import { createApikeyApi } from '@shared/core/api'

export const {
  getApiKeyList,
  getModelTypes,
  getUsageByModel,
  getUsageByKey,
  getUsageStats,
  getUsageTimeSeries,
  getUsageRecords,
} = createApikeyApi(request)
