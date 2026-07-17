import request from '@/api/request'
import type { UserInfo } from '@/types'

export function getCurrentUser(): Promise<UserInfo> {
  return request.get('/user/current') as unknown as Promise<UserInfo>
}
