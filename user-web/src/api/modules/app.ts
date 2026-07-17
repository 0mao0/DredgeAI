import request from '@/api/request'
import type { AppCard } from '@/types'

export function getAppList(): Promise<AppCard[]> {
  return request.get('/app/list') as unknown as Promise<AppCard[]>
}

export function getAppCategories(): Promise<{ key: string; label: string }[]> {
  return request.get('/app/categories') as unknown as Promise<{ key: string; label: string }[]>
}
