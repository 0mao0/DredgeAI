import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { UserInfo } from '@/types'
import { getCurrentUser } from '@/api/modules/user'

export const useUserStore = defineStore('user', () => {
  const userInfo = ref<UserInfo | null>(null)

  async function fetchUser(): Promise<void> {
    userInfo.value = await getCurrentUser()
  }

  /** 幂等加载（已加载则跳过），供权限守卫使用 */
  async function ensureUser(): Promise<void> {
    if (!userInfo.value) await fetchUser()
  }

  return { userInfo, fetchUser, ensureUser }
}, {
  persist: { pick: ['userInfo'] },
})
