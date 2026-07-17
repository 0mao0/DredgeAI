import request from '@/api/request'
import type { TaskItem } from '@/types'

export function getRecentTasks(): Promise<TaskItem[]> {
  return request.get('/task/recent') as unknown as Promise<TaskItem[]>
}

export function getQuickTasks(): Promise<{ id: string; title: string; tag: string; route: string; icon: string }[]> {
  return request.get('/task/quick') as unknown as Promise<{ id: string; title: string; tag: string; route: string; icon: string }[]>
}
