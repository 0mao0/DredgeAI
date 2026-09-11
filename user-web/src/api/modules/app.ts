import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { AppCard } from '@/types'

export function getAppList(): Promise<AppCard[]> {
  return request.get<AppCard[]>(urls.appList)
}

export interface UserAppOrderResult {
  routeIds: string[] | null
}

/** 获取当前用户的个性化应用顺序（未个性化返回 null） */
export function getUserAppOrder(): Promise<UserAppOrderResult> {
  return request.get<UserAppOrderResult>(urls.userAppOrder)
}

/** 保存当前用户的个性化应用顺序 */
export function saveUserAppOrder(routeIds: string[]): Promise<UserAppOrderResult> {
  return request.put<UserAppOrderResult>(urls.userAppOrder, { routeIds })
}
