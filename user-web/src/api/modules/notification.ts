import request from '@/api/request'
import type { Notification } from '@/types'

export function getNotifications(): Promise<Notification[]> {
  return request.get('/notification/list') as unknown as Promise<Notification[]>
}
