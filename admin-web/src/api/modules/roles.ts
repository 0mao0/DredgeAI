import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { Role, OrgUser } from '@/types'

export function getRoles(): Promise<Role[]> {
  return request.get<Role[]>(urls.roles)
}

export function createRole(data: Pick<Role, 'name'>): Promise<Role> {
  return request.post<Role>(urls.roles, data)
}

export function updateRole(id: string, data: Partial<Pick<Role, 'name'>>): Promise<void> {
  return request.put(urls.roleDetail.replace(':id', id), data)
}

export function deleteRole(id: string): Promise<void> {
  return request.delete(urls.roleDetail.replace(':id', id))
}

export function getRoleUsers(roleId: string): Promise<OrgUser[]> {
  return request.get<OrgUser[]>(urls.roleUsers.replace(':id', roleId))
}

export function addRoleUsers(roleId: string, userIds: string[]): Promise<void> {
  return request.post(urls.roleUsers.replace(':id', roleId), { userIds })
}

export function removeRoleUser(roleId: string, userId: string): Promise<void> {
  return request.delete(`${urls.roleUsers.replace(':id', roleId)}/${userId}`)
}

export interface RolePermissions {
  menuKeys: string[]
  appIds: string[]
}

export function setRolePermissions(roleId: string, data: RolePermissions): Promise<void> {
  return request.put(urls.rolePermissions.replace(':id', roleId), data)
}
