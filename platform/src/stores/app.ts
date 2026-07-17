import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AppCard, MetricCard, TaskItem, FileItem, UserInfo } from '@/types'
import { userInfo as mockUser, metricCards, appCards, taskItems, fileItems } from '@/mock/data'

export const useAppStore = defineStore('app', () => {
  const user = ref<UserInfo>(mockUser)
  const metrics = ref<MetricCard[]>(metricCards)
  const apps = ref<AppCard[]>(appCards)
  const tasks = ref<TaskItem[]>(taskItems)
  const files = ref<FileItem[]>(fileItems)

  return { user, metrics, apps, tasks, files }
})
