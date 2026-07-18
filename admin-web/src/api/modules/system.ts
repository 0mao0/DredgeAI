import request from '@/api/request'
import type { SystemLog } from '@/types'

export function getSystemLogs(): Promise<SystemLog[]> {
  return request.get<SystemLog[]>('/system/logs')
}
