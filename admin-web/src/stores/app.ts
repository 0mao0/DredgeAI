import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserInfo } from '@/types'
import type { ApplicationConfiguration } from '@shared/core/types/appConfig'
import { getProfile } from '@/api/modules/profile'
import { getApplicationConfiguration } from '@/api/modules/appConfig'

export const useAppStore = defineStore('app', () => {
  const profile = ref<UserInfo | null>(null)
  const appConfig = ref<ApplicationConfiguration | null>(null)
  let appConfigRequest: Promise<void> | null = null

  const isSuperAdmin = computed(() => profile.value?.roles.includes('admin') ?? false)

  const currentUser = computed(() => appConfig.value?.currentUser ?? null)

  const grantedPolicies = computed<Record<string, boolean>>(() => appConfig.value?.auth?.grantedPolicies ?? {})

  /** 当前用户权限码集合（唯一来源：应用配置 grantedPolicies），供权限守卫消费 */
  const permissions = computed<string[]>(() =>
    Object.entries(grantedPolicies.value).filter(([, v]) => v).map(([k]) => k),
  )

  function setProfile(user: UserInfo): void {
    profile.value = user
  }

  /** 幂等加载用户资料（已加载则跳过），供 Layout 与权限守卫共用 */
  async function fetchProfile(): Promise<void> {
    if (profile.value) return
    profile.value = await getProfile()
  }

  function isGranted(name: string): boolean {
    return grantedPolicies.value[name] === true
  }

  /** 幂等加载应用配置（已加载/加载中则复用），供路由守卫在首个受保护页前调用 */
  async function fetchAppConfig(): Promise<void> {
    if (appConfig.value) return
    if (!appConfigRequest) {
      appConfigRequest = getApplicationConfiguration()
        .then((cfg) => { appConfig.value = cfg })
        .finally(() => { appConfigRequest = null })
    }
    return appConfigRequest
  }

  return {
    profile,
    appConfig,
    isSuperAdmin,
    currentUser,
    grantedPolicies,
    permissions,
    setProfile,
    fetchProfile,
    isGranted,
    fetchAppConfig,
  }
})
