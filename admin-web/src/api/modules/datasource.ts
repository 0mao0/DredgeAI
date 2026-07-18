import request from '@/api/request'
import type { DataSource } from '@/types'

export function getDataSources(): Promise<DataSource[]> {
  return request.get<DataSource[]>('/datasources')
}
