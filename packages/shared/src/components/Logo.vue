<template>
  <div class="logo" :class="{ collapsed }">
    <div class="logo-mark">
      <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
        <polygon points="16,5.5 21.5,9.5 16,13.5 10.5,9.5" fill="white" opacity="0.95" class="logo-diamond" />
        <path d="M6,18 Q11,17 16,18 T26,18" stroke="white" stroke-width="1.5" fill="none" opacity="0.5" stroke-linecap="round" class="logo-wave" />
        <path d="M9,22.5 Q13,21.5 16,22.5 T23,22.5" stroke="white" stroke-width="1.5" fill="none" opacity="0.25" stroke-linecap="round" class="logo-wave" />
      </svg>
    </div>
    <div v-if="!collapsed" class="logo-text">
      <div class="logo-name">智浚 AI</div>
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
  width: 32px;
  height: 32px;
  border-radius: 7px;
  background: var(--color-brand-gradient);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12);
  transition: box-shadow @transition-base;
}

.logo-mark svg {
  width: 22px;
  height: 22px;
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
  line-height: 1.25;
  letter-spacing: 0.02em;
}

.logo-sub {
  font-size: 10px;
  color: @header-text-secondary;
  letter-spacing: 1.2px;
  text-transform: uppercase;
  line-height: 1.3;
}

.collapsed {
  justify-content: center;
  padding: 0 @spacing-md;

  .logo-mark {
    box-shadow: none;
  }
}
</style>
