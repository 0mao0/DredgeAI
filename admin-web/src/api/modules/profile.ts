import request from '@/api/request'
import type { UserInfo } from '@/types'

export function getProfile(): Promise<UserInfo> {
  return request.get<UserInfo>('/profile')
}
