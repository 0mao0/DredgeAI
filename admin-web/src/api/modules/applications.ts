import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { AppCategory, AppMainStatus, ApplicationItem, SubApp, SubAppStatus } from '@/types'

export interface ResetUserOrdersResult {
  count: number
}

export function getApplications(): Promise<ApplicationItem[]> {
  return request.get<ApplicationItem[]>(urls.applications)
}

/** 上移/下移主应用，返回重排后的应用目录 */
export function moveApplication(appId: string, direction: 'up' | 'down'): Promise<ApplicationItem[]> {
  return request.post<ApplicationItem[]>(urls.adminAppMove, { appId, direction })
}

/** 上移/下移子应用（母项组内），返回重排后的应用目录 */
export function moveSubApplication(subId: string, direction: 'up' | 'down'): Promise<ApplicationItem[]> {
  return request.post<ApplicationItem[]>(urls.adminSubAppMove, { subId, direction })
}

/** 清空所有用户的个性化顺序（管理员显式动作） */
export function resetUserOrders(): Promise<ResetUserOrdersResult> {
  return request.post<ResetUserOrdersResult>(urls.adminAppOrderReset)
}

/** 获取某模块已发布的子应用列表 */
export function getSubApps(appId: string): Promise<SubApp[]> {
  return request.get<SubApp[]>('/bidcompare/app-catalog/sub', { params: { appId } })
}

/** 设置某子应用的发布状态（发布 / 下架） */
export function setSubAppStatus(subId: string, status: SubAppStatus): Promise<void> {
  return request.post('/bidcompare/app-catalog/sub/status', { subId, status })
}

/** 设置某主应用的发布状态（运营中 / 已下架），决定其是否对用户开放 */
export function setApplicationStatus(appId: string, status: AppMainStatus): Promise<void> {
  return request.post('/bidcompare/app-catalog/status', { appId, status })
}

/** 设置应用类型/分类 */
export function setApplicationCategory(appId: string, category: AppCategory): Promise<void> {
  return request.post('/bidcompare/app-catalog/category', { appId, category })
}

export function setSubAppCategory(subId: string, category: AppCategory): Promise<void> {
  return request.post('/bidcompare/app-catalog/sub/category', { subId, category })
}

/** 设置主应用图标（antd 图标名） */
export function setApplicationIcon(appId: string, icon: string): Promise<void> {
  return request.post('/bidcompare/app-catalog/icon', { appId, icon })
}

/** 设置子应用图标（antd 图标名） */
export function setSubAppIcon(subId: string, icon: string): Promise<void> {
  return request.post('/bidcompare/app-catalog/sub/icon', { subId, icon })
}

/** 应用分类配置（枚举 wire 值 + 标签色，由 API 返回，前端不再硬编码；中文展示走 APP_CATEGORY_LABELS） */
export interface CategoryConfig {
  name: string
  color: string
}

export function getCategoryConfig(): Promise<CategoryConfig[]> {
  return request.get<CategoryConfig[]>('/bidcompare/app-catalog/categories')
}
