import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { PermissionItem } from '@/types'

export function getPermissions(): Promise<PermissionItem[]> {
  return request.get<PermissionItem[]>(urls.permissions)
}
