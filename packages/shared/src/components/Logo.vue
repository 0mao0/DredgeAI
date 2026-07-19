<template>
  <div class="logo" :class="{ collapsed }">
    <div class="logo-mark">
      <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
        <!-- 疏浚铲斗：切入水面的梯形铲头 -->
        <path d="M7 9.5 L25 9.5 L21.5 17.5 Q16 20.5 10.5 17.5 Z"
              fill="white" fill-opacity="0.96" />
        <!-- 铲斗内高光分割线 -->
        <path d="M7 9.5 L25 9.5 L23.2 13 L8.8 13 Z" fill="white" fill-opacity="0.28" />
        <!-- 双层水波 -->
        <path d="M4 22.5 Q8.5 20.5 13 22.5 T24 22.5"
              stroke="white" stroke-width="1.6" stroke-linecap="round" fill="none" stroke-opacity="0.55" />
        <path d="M6 26 Q10 24.3 13.5 26 T22 26"
              stroke="white" stroke-width="1.6" stroke-linecap="round" fill="none" stroke-opacity="0.3" />
      </svg>
    </div>
    <div v-if="!collapsed" class="logo-text">
      <div class="logo-name">智浚 <span class="logo-ai">AI</span></div>
      <div class="logo-sub">{{ subtitle }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  collapsed?: boolean
  subtitle?: string
}>(), {
  collapsed: false,
  subtitle: 'DredgeAI',
})
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.logo {
  height: @header-height;
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: 0 @spacing-xl;
}

.logo-mark {
  width: 34px;
  height: 34px;
  border-radius: 9px;
  background: var(--color-brand-gradient);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  box-shadow: var(--shadow-brand);
  transition: box-shadow @transition-base, transform @transition-base;
}

.logo-mark svg {
  width: 24px;
  height: 24px;
  display: block;
}

.logo-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.logo-name {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @header-text;
  line-height: 1.2;
  letter-spacing: 0.02em;
  display: flex;
  align-items: baseline;
  gap: 2px;
}

.logo-ai {
  background: var(--color-brand-gradient);
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
  font-weight: @font-weight-bold;
}

.logo-sub {
  font-size: 11px;
  color: @header-text-secondary;
  letter-spacing: 0.6px;
  line-height: 1.3;
  margin-top: 2px;
}

.collapsed {
  justify-content: center;
  padding: 0 @spacing-md;

  .logo-mark {
    box-shadow: none;
  }
}
</style>
