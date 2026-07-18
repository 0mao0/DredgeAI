import request from '@/api/request'
import type { TaskItem } from '@/types'

export interface QuickTask {
  id: string
  title: string
  tag: string
  route: string
  icon: string
}

export function getRecentTasks(): Promise<TaskItem[]> {
  return request.get<TaskItem[]>('/task/recent')
}

export function getQuickTasks(): Promise<QuickTask[]> {
  return request.get<QuickTask[]>('/task/quick')
}
