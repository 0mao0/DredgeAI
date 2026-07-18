import { ref, computed, watch } from 'vue'
import { theme } from 'ant-design-vue'
import type { ThemeConfig } from 'ant-design-vue/es/config-provider/context'

const { defaultAlgorithm, darkAlgorithm } = theme

const THEME_KEY = 'DREDGE_AI_USER_THEME'
type Theme = 'light' | 'dark'

const currentTheme = ref<Theme>((localStorage.getItem(THEME_KEY) as Theme) || 'light')

function applyTheme(t: Theme): void {
  document.documentElement.setAttribute('data-theme', t)
}

applyTheme(currentTheme.value)

export function useTheme() {
  function toggleTheme(): void {
    currentTheme.value = currentTheme.value === 'light' ? 'dark' : 'light'
  }

  watch(currentTheme, (val) => {
    localStorage.setItem(THEME_KEY, val)
    applyTheme(val)
  })

  const themeConfig = computed<ThemeConfig>(() => ({
    algorithm: currentTheme.value === 'dark' ? darkAlgorithm : defaultAlgorithm,
    token: {
      colorPrimary: '#0EA5E9',
      borderRadius: 8,
      fontSize: 14,
    },
  }))

  return { currentTheme, themeConfig, toggleTheme }
}
