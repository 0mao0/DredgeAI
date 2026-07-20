import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface DubbingNotice {
  id: string
  title: string
  taskId: string
  voiceName: string
  time: string
}

export const useDubbingNoticeStore = defineStore('dubbingNotice', () => {
  const notices = ref<DubbingNotice[]>([])

  function addNotice(taskId: string, voiceName: string): void {
    const now = new Date()
    notices.value.push({
      id: `notice-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      title: `配音任务 ${taskId} 已完成`,
      taskId,
      voiceName,
      time: now.toLocaleTimeString('zh-CN'),
    })
  }

  function removeNotice(id: string): void {
    notices.value = notices.value.filter((n) => n.id !== id)
  }

  function clearNotices(): void {
    notices.value = []
  }

  return { notices, addNotice, removeNotice, clearNotices }
}, {
  persist: { pick: ['notices'] },
})
