import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { ApplicationItem, SubApp } from '@/types'

export interface AppOrderResult {
  appIds: string[]
  /** 各母项应用下的子应用默认顺序（母项 id → 子应用 id 列表） */
  subOrders?: Record<string, string[]>
}

export interface ResetUserOrdersResult {
  count: number
}

export function getApplications(): Promise<ApplicationItem[]> {
  return request.get<ApplicationItem[]>(urls.applications)
}

/** 获取 admin 全局默认顺序（应用 id 列表） */
export function getAppOrder(): Promise<AppOrderResult> {
  return request.get<AppOrderResult>(urls.adminAppOrder)
}

/** 首次加载时用当前应用目录顺序播种默认顺序（含子应用分组顺序） */
export function seedAppOrder(appIds: string[], subOrders?: Record<string, string[]>): Promise<AppOrderResult> {
  return request.post<AppOrderResult>(urls.adminAppOrderSeed, { appIds, subOrders })
}

/** 上移/下移一个应用，返回重排后的默认顺序 */
export function moveAppOrder(appId: string, direction: 'up' | 'down'): Promise<AppOrderResult> {
  return request.post<AppOrderResult>(urls.adminAppOrderMove, { appId, direction })
}

/** 清空所有用户的个性化顺序（管理员显式动作） */
export function resetUserOrders(): Promise<ResetUserOrdersResult> {
  return request.post<ResetUserOrdersResult>(urls.adminAppOrderReset)
}

/** 获取某模块已发布的子应用列表 */
export function getSubApps(appId: string): Promise<SubApp[]> {
  return request.get<SubApp[]>('/applications/sub', { params: { appId } })
}

/** 设置某子应用的发布状态（发布 / 下架） */
export function setSubAppStatus(subId: string, status: '已发布' | '已下架'): Promise<void> {
  return request.post('/applications/sub/status', { subId, status })
}

/** 设置某主应用的发布状态（运营中 / 已下架），决定其是否对用户开放 */
export function setApplicationStatus(appId: string, status: '运营中' | '已下架'): Promise<void> {
  return request.post('/applications/status', { appId, status })
}

/** 设置应用类型/分类 */
export function setApplicationCategory(appId: string, category: string): Promise<void> {
  return request.post('/applications/category', { appId, category })
}

export function setSubAppCategory(subId: string, category: string): Promise<void> {
  return request.post('/applications/sub/category', { subId, category })
}

/** 设置主应用图标（antd 图标名） */
export function setApplicationIcon(appId: string, icon: string): Promise<void> {
  return request.post('/applications/icon', { appId, icon })
}

/** 设置子应用图标（antd 图标名） */
export function setSubAppIcon(subId: string, icon: string): Promise<void> {
  return request.post('/applications/sub/icon', { subId, icon })
}

/** 设置主应用授权范围（所有 / 部分） */
export function setApplicationScope(appId: string, scope: '所有' | '部分'): Promise<void> {
  return request.post('/applications/scope', { appId, scope })
}

/** 设置子应用授权范围（所有 / 部分） */
export function setSubAppScope(subId: string, scope: '所有' | '部分'): Promise<void> {
  return request.post('/applications/sub/scope', { subId, scope })
}

/** 应用分类配置（类型名 + 标签色，由 API 返回，前端不再硬编码） */
export interface CategoryConfig {
  name: string
  color: string
}

export function getCategoryConfig(): Promise<CategoryConfig[]> {
  return request.get<CategoryConfig[]>('/applications/categories')
}

/** 采集分类配置：按分类发布为子应用 */
export interface CollectionCategory {
  key: string
  name: string
  description: string
  published: boolean
  subAppId?: string
}

export function getCollectionCategories(appId: string): Promise<CollectionCategory[]> {
  return request.get<CollectionCategory[]>('/applications/collection-categories', { params: { appId } })
}

export function publishCollectionCategory(appId: string, categoryKey: string): Promise<SubApp> {
  return request.post<SubApp>('/applications/collection-categories/publish', { appId, categoryKey })
}
