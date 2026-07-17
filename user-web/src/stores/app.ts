import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AppCard, TaskItem, FileItem } from '@/types'
import { getAppList, getAppCategories } from '@/api/modules/app'
import { getRecentTasks, getQuickTasks } from '@/api/modules/task'
import { getRecentFiles } from '@/api/modules/file'

export const useAppStore = defineStore('app', () => {
  const apps = ref<AppCard[]>([])
  const categories = ref<{ key: string; label: string }[]>([])
  const tasks = ref<TaskItem[]>([])
  const quickTasks = ref<{ id: string; title: string; tag: string; route: string; icon: string }[]>([])
  const files = ref<FileItem[]>([])
  const sidebarCollapsed = ref(false)

  async function fetchApps(): Promise<void> { apps.value = await getAppList() }
  async function fetchCategories(): Promise<void> { categories.value = await getAppCategories() }
  async function fetchTasks(): Promise<void> { tasks.value = await getRecentTasks() }
  async function fetchQuickTasks(): Promise<void> { quickTasks.value = await getQuickTasks() }
  async function fetchFiles(): Promise<void> { files.value = await getRecentFiles() }
  function toggleSidebar(): void { sidebarCollapsed.value = !sidebarCollapsed.value }

  return {
    apps, categories, tasks, quickTasks, files, sidebarCollapsed,
    fetchApps, fetchCategories, fetchTasks, fetchQuickTasks, fetchFiles, toggleSidebar,
  }
}, {
  persist: { pick: ['sidebarCollapsed'] },
})
