import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { UserInfo, Notification } from '@/types'
import { getCurrentUser } from '@/api/modules/user'
import { getNotifications } from '@/api/modules/notification'

export const useUserStore = defineStore('user', () => {
  const userInfo = ref<UserInfo | null>(null)
  const notifications = ref<Notification[]>([])
  const unreadCount = ref(0)

  async function fetchUser(): Promise<void> {
    userInfo.value = await getCurrentUser()
  }

  async function fetchNotifications(): Promise<void> {
    notifications.value = await getNotifications()
    unreadCount.value = notifications.value.filter((n) => !n.read).length
  }

  return { userInfo, notifications, unreadCount, fetchUser, fetchNotifications }
}, {
  persist: { pick: ['userInfo'] },
})
