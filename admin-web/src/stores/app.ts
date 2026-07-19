import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserInfo } from '@/types'

export const useAppStore = defineStore('app', () => {
  const profile = ref<UserInfo | null>(null)

  const isSuperAdmin = computed(() => profile.value?.role === 'super_admin')

  function setProfile(user: UserInfo): void {
    profile.value = user
  }

  return {
    profile,
    isSuperAdmin,
    setProfile,
  }
})
