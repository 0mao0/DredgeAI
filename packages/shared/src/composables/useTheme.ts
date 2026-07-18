import { ref, computed, watch } from 'vue'
import { theme } from 'ant-design-vue'
import type { ThemeConfig } from 'ant-design-vue/es/config-provider/context'

const { defaultAlgorithm, darkAlgorithm } = theme

const THEME_KEY = 'DREDGE_AI_ADMIN_THEME'
type Theme = 'light' | 'dark'

const currentTheme = ref<Theme>((localStorage.getItem(THEME_KEY) as Theme) || 'light')

function applyTheme(theme: Theme): void {
  document.documentElement.setAttribute('data-theme', theme)
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
      colorBgContainer: currentTheme.value === 'dark' ? '#1E293B' : '#FFFFFF',
      colorBgLayout: currentTheme.value === 'dark' ? '#0B1220' : '#F8FAFC',
      colorTextBase: currentTheme.value === 'dark' ? '#F1F5F9' : '#0F172A',
      colorBorder: currentTheme.value === 'dark' ? '#334155' : '#E2E8F0',
      colorBgElevated: currentTheme.value === 'dark' ? '#1E293B' : '#FFFFFF',
    },
  }))

  return { currentTheme, themeConfig, toggleTheme }
}
