import { usePagePermissions as useSharedPagePermissions } from '@shared/web'
import { useAppStore } from '@/stores/app'

/** admin 页面按钮权限：读当前路由 meta.actionPermissions，按 grantedPolicies 判定（'*' 通配） */
export function usePagePermissions() {
  const appStore = useAppStore()
  return useSharedPagePermissions(
    (code) => appStore.permissions.includes('*') || appStore.isGranted(code),
  )
}
