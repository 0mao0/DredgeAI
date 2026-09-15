import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { PagedResult } from '@shared/core/types'

// ---- 代理路由 ----

export interface ProxyRouteMatch {
  path?: string
  hosts?: string[]
}

/** 代理路由列表项（对应后端 ProxyRouteDto） */
export interface ProxyRouteItem {
  id: string
  routeId: string
  description?: string
  clusterId: string
  authorizationPolicy?: string
  order: number
  match: ProxyRouteMatch
  isEnabled: boolean
  creationTime: string
}

/** 路由新增/编辑表单（对应后端 ProxyRouteCreateUpdateDto） */
export interface ProxyRouteFormData {
  routeId: string
  description?: string
  clusterId: string
  authorizationPolicy?: string
  order: number
  match: ProxyRouteMatch
  isEnabled: boolean
}

export interface ProxyRouteListParams {
  keyword?: string
  clusterId?: string
  skipCount: number
  maxResultCount: number
}

export function getProxyRoutes(params: ProxyRouteListParams): Promise<PagedResult<ProxyRouteItem>> {
  return request.get<PagedResult<ProxyRouteItem>>(urls.gatewayProxyRoutes, { params })
}

export function createProxyRoute(data: ProxyRouteFormData): Promise<ProxyRouteItem> {
  return request.post<ProxyRouteItem>(urls.gatewayProxyRoutes, data)
}

export function updateProxyRoute(id: string, data: ProxyRouteFormData): Promise<ProxyRouteItem> {
  return request.put<ProxyRouteItem>(urls.gatewayProxyRoute.replace(':id', id), data)
}

export function deleteProxyRoute(id: string): Promise<void> {
  return request.delete(urls.gatewayProxyRoute.replace(':id', id))
}

// ---- 代理集群 ----

export interface ClusterDestination {
  address: string
  health?: string
}

/** 代理集群列表项（对应后端 ProxyClusterDto） */
export interface ProxyClusterItem {
  id: string
  clusterId: string
  description?: string
  destinations: Record<string, ClusterDestination>
  isEnabled: boolean
  creationTime: string
}

/** 集群新增/编辑表单（对应后端 ProxyClusterCreateUpdateDto） */
export interface ProxyClusterFormData {
  clusterId: string
  description?: string
  destinations: Record<string, ClusterDestination>
  isEnabled: boolean
}

export async function getProxyClusters(): Promise<ProxyClusterItem[]> {
  // 集群列表不分页：ABP ListResultDto 包装为 { items }
  const res = await request.get<{ items: ProxyClusterItem[] }>(urls.gatewayProxyClusters)
  return res.items
}

export function createProxyCluster(data: ProxyClusterFormData): Promise<ProxyClusterItem> {
  return request.post<ProxyClusterItem>(urls.gatewayProxyClusters, data)
}

export function updateProxyCluster(id: string, data: ProxyClusterFormData): Promise<ProxyClusterItem> {
  return request.put<ProxyClusterItem>(urls.gatewayProxyCluster.replace(':id', id), data)
}

export function deleteProxyCluster(id: string): Promise<void> {
  return request.delete(urls.gatewayProxyCluster.replace(':id', id))
}
