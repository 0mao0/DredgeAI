<template>
  <a-config-provider :theme="themeConfig">
    <router-view />
  </a-config-provider>
</template>

<script setup lang="ts">
import { useTheme } from '@shared/composables/useTheme'

const { themeConfig } = useTheme()
</script>
