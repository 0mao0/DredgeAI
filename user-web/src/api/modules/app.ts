import request from '@/api/request'
import type { AppCard } from '@/types'

export function getAppList(): Promise<AppCard[]> {
  return request.get<AppCard[]>('/app/list')
}
