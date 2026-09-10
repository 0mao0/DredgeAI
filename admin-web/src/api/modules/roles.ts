import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { PagedResult } from '@shared/core/types'
import type { OrgUserItem } from './org-users'
import { getOrgUsers } from './org-users'

/** 角色列表项（对应后端 IdentityRoleDto + user-counts 合并） */
export interface RoleItem {
  id: string
  name: string
  isDefault: boolean
  isStatic: boolean
  isPublic: boolean
  concurrencyStamp: string
  creationTime: string
  userCount: number
}

export interface RoleListParams {
  filter?: string
  skipCount: number
  maxResultCount: number
}

export function getRoles(params: RoleListParams): Promise<PagedResult<RoleItem>> {
  return request.get<PagedResult<RoleItem>>(urls.roles, { params })
}

export function createRole(name: string): Promise<RoleItem> {
  return request.post<RoleItem>(urls.roles, { name, isDefault: false, isPublic: true })
}

export function updateRole(role: RoleItem, name: string): Promise<RoleItem> {
  return request.put<RoleItem>(urls.roleDetail.replace(':id', role.id), {
    name,
    isDefault: role.isDefault,
    isPublic: role.isPublic,
    concurrencyStamp: role.concurrencyStamp,
  })
}

export function deleteRole(id: string): Promise<void> {
  return request.delete(urls.roleDetail.replace(':id', id))
}

export interface RoleUserCount {
  roleName: string
  userCount: number
}

export function getRoleUserCounts(): Promise<RoleUserCount[]> {
  return request.get<RoleUserCount[]>(urls.roleUserCounts)
}

export function getRoleUsers(roleName: string): Promise<OrgUserItem[]> {
  return getOrgUsers({ roleName, maxResultCount: 1000 }).then((res) => res.items)
}

/** 全量替换角色成员（按角色名；后端做差量增删） */
export function setRoleUsers(roleName: string, userIds: string[]): Promise<void> {
  return request.post(urls.roleBatchSetUsers, { roleName, userIds })
}

export function removeRoleUser(roleName: string, userId: string): Promise<void> {
  return request.delete(urls.roleRemoveUser, { params: { roleName, userId } })
}

// ---- 菜单/按钮权限（ABP 授权） ----

export interface PermissionGrantItem {
  name: string
  isGranted: boolean
}

interface PermissionListResult {
  entityDisplayName: string
  groups: { name: string, permissions: PermissionGrantItem[] }[]
}

/** 读取角色已授予的权限码列表（providerName=R，providerKey=角色名） */
export async function getRoleGrantedPermissions(roleName: string): Promise<string[]> {
  const res = await request.get<PermissionListResult>(urls.rolePermissions, {
    params: { providerName: 'R', providerKey: roleName },
  })
  return res.groups
    .flatMap((g) => g.permissions)
    .filter((p) => p.isGranted)
    .map((p) => p.name)
}

/** 全量提交角色授权（granted 须覆盖所有已知权限码，未授予的传 isGranted=false） */
export function setRolePermissions(roleName: string, granted: PermissionGrantItem[]): Promise<void> {
  return request.put(
    urls.rolePermissions,
    { permissions: granted },
    { params: { providerName: 'R', providerKey: roleName } },
  )
}
