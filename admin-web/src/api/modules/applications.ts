import request from '@/api/request'
import type { ApplicationItem, SubApp } from '@/types'

export function getApplications(): Promise<ApplicationItem[]> {
  return request.get<ApplicationItem[]>('/applications')
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
