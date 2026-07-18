import request from '@/api/request'
import type { PermissionItem } from '@/types'

export function getPermissions(): Promise<PermissionItem[]> {
  return request.get('/permissions')
}
