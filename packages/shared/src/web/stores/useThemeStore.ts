import { defineStore } from 'pinia'
import 'pinia-plugin-persistedstate'
import { ref, computed, watch } from 'vue'

/** 主题模式：light / dark / auto（跟随系统） */
export type Theme = 'light' | 'dark' | 'auto'

/** 统一 storage key，双端共用 */
const THEME_KEY = 'DREDGE_AI_THEME'

/** 系统色彩方案媒体查询（惰性获取，SSR 安全） */
function getColorSchemeQuery(): MediaQueryList | null {
  if (typeof window === 'undefined' || !window.matchMedia) return null
  return window.matchMedia('(prefers-color-scheme: dark)')
}

/** 将 theme 解析为实际生效的 light/dark */
export function resolveEffectiveTheme(t: Theme): 'light' | 'dark' {
  if (t === 'auto') {
    return getColorSchemeQuery()?.matches ? 'dark' : 'light'
  }
  return t
}

/** 统一主题 store：双端共用，替代 user-web 旧 theme store 与 admin-web 的 useTheme 内部状态 */
export const useThemeStore = defineStore('theme', () => {
  const theme = ref<Theme>((localStorage.getItem(THEME_KEY) as Theme) || 'light')

  const effectiveTheme = computed(() => resolveEffectiveTheme(theme.value))
  const isDark = computed(() => effectiveTheme.value === 'dark')

  /** 应用主题到 document.documentElement */
  function applyTheme(): void {
    if (typeof document === 'undefined') return
    document.documentElement.setAttribute('data-theme', effectiveTheme.value)
  }

  /** 设置主题 */
  function setTheme(t: Theme): void {
    theme.value = t
  }

  /** 在 light/dark 之间切换（auto 视为当前生效值） */
  function toggleTheme(): void {
    theme.value = isDark.value ? 'light' : 'dark'
  }

  // 监听系统色彩方案变化（auto 模式下自动切换）
  const query = getColorSchemeQuery()
  if (query) {
    query.addEventListener('change', () => {
      if (theme.value === 'auto') applyTheme()
    })
  }

  // 持久化 + 应用
  watch(theme, () => {
    localStorage.setItem(THEME_KEY, theme.value)
    applyTheme()
  }, { immediate: true })

  return { theme, effectiveTheme, isDark, setTheme, toggleTheme }
}, {
  persist: { pick: ['theme'] },
})
