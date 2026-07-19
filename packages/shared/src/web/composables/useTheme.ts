import { computed } from 'vue'
import { theme } from 'ant-design-vue'
import type { ThemeConfig } from 'ant-design-vue/es/config-provider/context'
import { useThemeStore } from '../stores/useThemeStore'

const { defaultAlgorithm, darkAlgorithm } = theme

/**
 * 主题 composable：基于共享 useThemeStore 提供 antd ThemeConfig 与切换能力。
 * 双端共用，替代原 admin-web 内联的 currentTheme 状态。
 */
export function useTheme() {
  const themeStore = useThemeStore()

  const themeConfig = computed<ThemeConfig>(() => {
    const isDark = themeStore.isDark
    return {
      algorithm: isDark ? darkAlgorithm : defaultAlgorithm,
      token: {
        colorPrimary: isDark ? '#60A5FA' : '#0EA5E9',
        borderRadius: 8,
        fontSize: 14,
        colorBgContainer: isDark ? '#141C2C' : '#FFFFFF',
        colorBgLayout: isDark ? '#0B1220' : '#F6F3EF',
        colorTextBase: isDark ? '#E2E8F0' : '#1C1917',
        colorBorder: isDark ? '#1E2A3E' : '#E5DFD8',
        colorBgElevated: isDark ? '#1A2438' : '#FFFFFF',
      },
    }
  })

  return {
    currentTheme: computed(() => themeStore.effectiveTheme),
    themeConfig,
    toggleTheme: themeStore.toggleTheme,
  }
}
