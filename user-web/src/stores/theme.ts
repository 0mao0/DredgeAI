import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

const THEME_KEY = 'DREDGE_AI_USER_THEME'
export type Theme = 'light' | 'dark' | 'auto'

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<Theme>((localStorage.getItem(THEME_KEY) as Theme) || 'light')

  watch(theme, (val) => {
    localStorage.setItem(THEME_KEY, val)
    applyTheme(val)
  }, { immediate: true })

  function applyTheme(t: Theme): void {
    const effective = t === 'auto'
      ? window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
      : t
    document.documentElement.setAttribute('data-theme', effective)
  }

  return { theme }
}, {
  persist: { pick: ['theme'] },
})
