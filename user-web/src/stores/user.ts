import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { UserInfo } from '@/types'
import { getCurrentUser } from '@/api/modules/user'

export const useUserStore = defineStore('user', () => {
  const userInfo = ref<UserInfo | null>(null)

  async function fetchUser(): Promise<void> {
    userInfo.value = await getCurrentUser()
  }

  return { userInfo, fetchUser }
}, {
  persist: { pick: ['userInfo'] },
})
