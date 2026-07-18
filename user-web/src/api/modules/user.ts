import request from '@/api/request'
import type { UserInfo } from '@/types'

export function getCurrentUser(): Promise<UserInfo> {
  return request.get<UserInfo>('/user/current')
}
