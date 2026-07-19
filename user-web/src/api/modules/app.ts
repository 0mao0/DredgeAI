import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { AppCard } from '@/types'

export function getAppList(): Promise<AppCard[]> {
  return request.get<AppCard[]>(urls.appList)
}
