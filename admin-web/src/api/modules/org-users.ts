import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { PagedResult } from '@shared/core/types'

/** 组织单位简要信息（对应后端 OrganizationUnitBriefDto） */
export interface OrgUnitBrief {
  key: string
  name: string
}

/** 组织用户列表项（对应后端 UserDto） */
export interface OrgUserItem {
  id: string
  userName: string
  name: string
  phoneNumber: string | null
  email: string | null
  isActive: boolean
  expireTime: string | null
  organizationUnits: OrgUnitBrief[]
  roleNames: string[]
  creationTime: string
}

export interface OrgUserListParams {
  keyword?: string
  isActive?: boolean
  skipCount?: number
  maxResultCount?: number
}

/** 角色选项（对应后端 IdentityRoleDto 子集） */
export interface RoleOption {
  id: string
  name: string
}

export function getOrgUsers(params: OrgUserListParams): Promise<PagedResult<OrgUserItem>> {
  return request.get<PagedResult<OrgUserItem>>(urls.orgUsers, { params })
}

/** 启停用户登录（isActive 为 query 参数，对应 change-active 端点） */
export function setUserActive(id: string, isActive: boolean): Promise<void> {
  return request.put(urls.orgUserChangeActive.replace(':id', id), null, { params: { isActive } })
}

/** 全量替换用户角色（按角色名；后端做差量增删） */
export function updateUserRoles(id: string, roleNames: string[]): Promise<void> {
  return request.put(urls.orgUserDetail.replace(':id', id), { roleNames })
}

export function getAllRoleOptions(): Promise<RoleOption[]> {
  return request.get<{ items: RoleOption[] }>(urls.orgRoleAll).then((res) => res.items)
}

export interface CreateOrgUserParams {
  userName: string
  name: string
  phoneNumber: string
  password: string
  organizationIds?: string[]
}

export interface UpdateOrgUserParams {
  name?: string
  phoneNumber?: string
  organizationIds?: string[]
}

export function createOrgUser(data: CreateOrgUserParams): Promise<OrgUserItem> {
  return request.post<OrgUserItem>(urls.orgUsers, data)
}

export function updateOrgUser(id: string, data: UpdateOrgUserParams): Promise<OrgUserItem> {
  return request.put<OrgUserItem>(urls.orgUserDetail.replace(':id', id), data)
}

export function deleteOrgUser(id: string): Promise<void> {
  return request.delete(urls.orgUserDetail.replace(':id', id))
}
