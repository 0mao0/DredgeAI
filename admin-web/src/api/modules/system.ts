import request from '@/api/request'
import type { SystemLog } from '@/types'

export function getSystemLogs(): Promise<SystemLog[]> {
  return request.get('/system/logs')
}
