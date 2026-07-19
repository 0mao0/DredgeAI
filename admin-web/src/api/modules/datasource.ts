import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { DataSource } from '@/types'

export function getDataSources(): Promise<DataSource[]> {
  return request.get<DataSource[]>(urls.datasources)
}
