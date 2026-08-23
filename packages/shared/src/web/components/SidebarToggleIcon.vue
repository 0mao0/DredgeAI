<template>
  <svg
    viewBox="0 0 24 24"
    width="18"
    height="18"
    fill="none"
    stroke="currentColor"
    stroke-width="1.6"
    stroke-linecap="round"
    aria-hidden="true"
  >
    <template v-if="collapsed">
      <path d="M10.5 4.5H7.5a3 3 0 0 0-3 3v9a3 3 0 0 0 3 3h3V4.5Z" fill="currentColor" stroke="none" />
    </template>
    <rect x="4.5" y="4.5" width="15" height="15" rx="3" />
    <line x1="10.5" y1="4.5" x2="10.5" y2="19.5" />
  </svg>
</template>

<script setup lang="ts">
defineProps<{
  collapsed: boolean
}>()
</script>
