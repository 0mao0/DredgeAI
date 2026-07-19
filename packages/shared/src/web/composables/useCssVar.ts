import { ref, onScopeDispose } from 'vue'

/**
 * 响应式读取 CSS 变量值，主题切换（data-theme 属性变化）时自动更新
 */
export function useCssVar(name: string) {
  const val = ref(getComputedStyle(document.documentElement).getPropertyValue(name).trim())

  // 通过 MutationObserver 监听 data-theme 属性变化，与具体主题实现解耦
  const observer = new MutationObserver(() => {
    val.value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  })

  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['data-theme'],
  })

  onScopeDispose(() => observer.disconnect())

  return val
}

/**
 * 同步读取 CSS 变量值（非响应式）
 */
export function cssVarValue(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}
