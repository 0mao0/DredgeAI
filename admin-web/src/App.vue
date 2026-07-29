<template>
  <a-config-provider :theme="themeConfig" :locale="zhCN">
    <ErrorBoundary>
      <router-view />
    </ErrorBoundary>
  </a-config-provider>
</template>

<script setup lang="ts">
import { useTheme } from '@shared/web/composables/useTheme'
import ErrorBoundary from '@shared/web/components/ErrorBoundary.vue'
import zhCN from 'ant-design-vue/es/locale/zh_CN'

const { themeConfig } = useTheme()
</script>
