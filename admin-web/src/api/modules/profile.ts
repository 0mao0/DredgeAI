import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { UserInfo } from '@/types'

export function getProfile(): Promise<UserInfo> {
  return request.get<UserInfo>(urls.adminProfile)
}
