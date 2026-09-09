import { computed } from 'vue'
import { useRoute } from 'vue-router'

/** 权限判定函数：输入权限码，返回是否已授权 */
export type PagePermissionChecker = (code: string) => boolean

/**
 * 判定某个操作是否可用（纯函数，供测试与 composable 复用）：
 * - 操作未在当前路由声明 → 放行（与现状一致，页面级守卫兜底）
 * - 已声明 → 按权限码判定
 */
export function isActionGranted(
  actionPermissions: Record<string, string> | undefined,
  action: string,
  isGranted: PagePermissionChecker,
): boolean {
  const code = actionPermissions?.[action]
  if (!code) return true
  return isGranted(code)
}

/** 页面按钮权限：读取当前路由 meta.actionPermissions，暴露 can(action) 供模板 v-if 使用 */
export function usePagePermissions(isGranted: PagePermissionChecker) {
  const route = useRoute()
  const actionPermissions = computed(() => route.meta.actionPermissions ?? {})
  const can = (action: string): boolean => isActionGranted(actionPermissions.value, action, isGranted)
  return { can, actionPermissions }
}
