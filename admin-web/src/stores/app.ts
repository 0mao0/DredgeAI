import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserInfo } from '@/types'
import { getProfile } from '@/api/modules/profile'

export const useAppStore = defineStore('app', () => {
  const profile = ref<UserInfo | null>(null)

  const isSuperAdmin = computed(() => profile.value?.roles.includes('admin') ?? false)

  /** 角色 → 权限码集合（'*' 通配全部），供权限守卫消费 */
  const permissions = computed<string[]>(() => (profile.value?.roles.includes('admin') ? ['*'] : []))

  function setProfile(user: UserInfo): void {
    profile.value = user
  }

  /** 幂等加载用户资料（已加载则跳过），供 Layout 与权限守卫共用 */
  async function fetchProfile(): Promise<void> {
    if (profile.value) return
    profile.value = await getProfile()
  }

  return {
    profile,
    isSuperAdmin,
    permissions,
    setProfile,
    fetchProfile,
  }
})
