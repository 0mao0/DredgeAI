<template>
  <svg class="ship-ai-logo" viewBox="0 0 32 32" width="24" height="24" fill="none" aria-hidden="true">
    <defs>
      <linearGradient id="dredge-ship-ai-grad" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stop-color="var(--color-brand)" />
        <stop offset="1" stop-color="var(--color-accent)" />
      </linearGradient>
    </defs>
    <!-- 波浪 -->
    <path
      d="M3 24.5q4-2.6 8 0t8 0t8 0"
      stroke="var(--color-brand)"
      stroke-width="1.7"
      stroke-linecap="round"
    />
    <path
      d="M5 28q4-2.6 8 0t8 0t6 0"
      stroke="var(--color-brand)"
      stroke-width="1.7"
      stroke-linecap="round"
      opacity="0.45"
    />
    <!-- 船体 -->
    <path d="M6 20h20l-2.8 5H8.8z" fill="url(#dredge-ship-ai-grad)" />
    <!-- 上层建筑 -->
    <rect x="13" y="15.5" width="6" height="4.5" rx="1" fill="url(#dredge-ship-ai-grad)" />
    <!-- 桅杆 -->
    <path d="M16 15.5v-5" stroke="var(--color-brand)" stroke-width="1.6" stroke-linecap="round" />
    <!-- AI 神经节点 -->
    <circle cx="16" cy="8" r="2.1" fill="var(--color-brand)" />
    <circle cx="22" cy="5.5" r="1.5" fill="var(--color-brand)" opacity="0.65" />
    <circle cx="10.5" cy="5.5" r="1.5" fill="var(--color-brand)" opacity="0.65" />
    <path d="M14.4 7.4 11.8 6.1M17.6 7.4l3.4-1.6" stroke="var(--color-brand)" stroke-width="1" opacity="0.7" />
  </svg>
</template>

<style scoped lang="less">
.ship-ai-logo {
  display: block;
  flex-shrink: 0;
}
</style>
