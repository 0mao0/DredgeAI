<template>
  <a-config-provider :theme="themeConfig">
    <ErrorBoundary>
      <router-view />
    </ErrorBoundary>
  </a-config-provider>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useTheme } from '@shared/web/composables/useTheme'
import ErrorBoundary from '@shared/web/components/ErrorBoundary.vue'
import { useAuthStore } from '@/stores/auth'

const { themeConfig } = useTheme()

onMounted(() => {
  // 恢复 cookie 登录态：需要续期则立即静默刷新，否则按过期时刻排定自动刷新
  useAuthStore().init()
})
</script>
