<template>
  <a-config-provider :theme="themeConfig">
    <ErrorBoundary>
      <router-view />
    </ErrorBoundary>
  </a-config-provider>
</template>

<script setup lang="ts">
import { useTheme } from '@shared/web/composables/useTheme'
import ErrorBoundary from '@shared/web/components/ErrorBoundary.vue'

const { themeConfig } = useTheme()
</script>
