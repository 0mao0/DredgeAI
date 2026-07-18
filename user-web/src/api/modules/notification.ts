import request from '@/api/request'
import type { Notification } from '@/types'

export function getNotifications(): Promise<Notification[]> {
  return request.get<Notification[]>('/notification/list')
}
