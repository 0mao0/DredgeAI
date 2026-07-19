import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { TaskItem, QuickTask } from '@/types'

export type { QuickTask }

export function getRecentTasks(): Promise<TaskItem[]> {
  return request.get<TaskItem[]>(urls.taskRecent)
}

export function getQuickTasks(): Promise<QuickTask[]> {
  return request.get<QuickTask[]>(urls.taskQuick)
}
