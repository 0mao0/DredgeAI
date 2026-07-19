<template>
  <a-config-provider :theme="themeConfig">
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { theme } from 'ant-design-vue'
import { useThemeStore } from '@/stores/theme'

const { defaultAlgorithm, darkAlgorithm } = theme
const themeStore = useThemeStore()

// 计算当前生效的主题（auto 模式下根据系统偏好）
const effectiveTheme = computed(() => {
  if (themeStore.theme === 'auto') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  }
  return themeStore.theme
})

// 根据生效主题生成 antd 主题配置
const themeConfig = computed(() => ({
  algorithm: effectiveTheme.value === 'dark' ? darkAlgorithm : defaultAlgorithm,
  token: {
    colorPrimary: effectiveTheme.value === 'dark' ? '#60A5FA' : '#0EA5E9',
    borderRadius: 8,
    fontSize: 14,
    colorBgContainer: effectiveTheme.value === 'dark' ? '#141C2C' : '#FFFFFF',
    colorBgLayout: effectiveTheme.value === 'dark' ? '#0B1220' : '#F6F3EF',
    colorTextBase: effectiveTheme.value === 'dark' ? '#E2E8F0' : '#1C1917',
    colorBorder: effectiveTheme.value === 'dark' ? '#1E2A3E' : '#E5DFD8',
    colorBgElevated: effectiveTheme.value === 'dark' ? '#1A2438' : '#FFFFFF',
  },
}))
</script>