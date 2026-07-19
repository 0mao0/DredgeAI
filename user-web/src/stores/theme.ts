import { defineStore } from 'pinia'
import { ref, watch, onUnmounted } from 'vue'

const THEME_KEY = 'DREDGE_AI_USER_THEME'
export type Theme = 'light' | 'dark' | 'auto'

const colorSchemeQuery = window.matchMedia('(prefers-color-scheme: dark)')

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<Theme>((localStorage.getItem(THEME_KEY) as Theme) || 'light')

  // 应用主题到 DOM
  function applyTheme(t: Theme): void {
    const effective = t === 'auto'
      ? (colorSchemeQuery.matches ? 'dark' : 'light')
      : t
    document.documentElement.setAttribute('data-theme', effective)
  }

  // 监听系统色彩方案变化（auto 模式下自动切换）
  function onSystemPreferenceChange(): void {
    if (theme.value === 'auto') {
      applyTheme('auto')
    }
  }
  colorSchemeQuery.addEventListener('change', onSystemPreferenceChange)

  watch(theme, (val) => {
    localStorage.setItem(THEME_KEY, val)
    applyTheme(val)
  }, { immediate: true })

  // 清理监听
  if (typeof onUnmounted === 'function') {
    onUnmounted(() => {
      colorSchemeQuery.removeEventListener('change', onSystemPreferenceChange)
    })
  }

  return { theme }
}, {
  persist: { pick: ['theme'] },
})