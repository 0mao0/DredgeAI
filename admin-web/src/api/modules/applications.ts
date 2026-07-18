import request from '@/api/request'
import type { ApplicationItem } from '@/types'

export function getApplications(): Promise<ApplicationItem[]> {
  return request.get('/applications')
}
