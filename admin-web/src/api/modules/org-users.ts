import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { OrgUser } from '@/types'

export interface OrgUserListParams {
  keyword?: string
  status?: string
  page?: number
  pageSize?: number
}

export interface PaginatedResult<T> {
  items: T[]
  total: number
}

export function getOrgUsers(params?: OrgUserListParams): Promise<PaginatedResult<OrgUser>> {
  return request.get<PaginatedResult<OrgUser>>(urls.orgUsers, { params })
}

export function setUserStatus(id: string, status: 'active' | 'disabled'): Promise<void> {
  return request.put(urls.orgUserStatus.replace(':id', id), { status })
}

export function setUserRoles(id: string, roleIds: string[]): Promise<void> {
  return request.put(urls.orgUserRoles.replace(':id', id), { roleIds })
}
