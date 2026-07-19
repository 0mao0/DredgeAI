import type { App, Directive, DirectiveBinding } from 'vue'

/** v-permission 指令参数：字符串或字符串数组 */
type PermissionValue = string | string[]

/** 权限判定函数：由各端注入实现（如读取用户权限码集合） */
export type PermissionChecker = (value: PermissionValue) => boolean

/** 默认权限检查器：恒为 true（未注入时退化为无限制） */
let checker: PermissionChecker = () => true

/** 注入应用级权限判定函数 */
export function setPermissionChecker(fn: PermissionChecker): void {
  checker = fn
}

/** v-permission：当用户不具备指定权限时移除元素 */
const permissionDirective: Directive<HTMLElement, PermissionValue> = {
  mounted(el: HTMLElement, binding: DirectiveBinding<PermissionValue>) {
    if (!checker(binding.value)) {
      el.parentNode?.removeChild(el)
    }
  },
}

/** 在 Vue 应用中注册权限指令 */
export function registerPermissionDirective(app: App): void {
  app.directive('permission', permissionDirective)
}
