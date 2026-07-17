<template>
  <div class="metric-card">
    <div class="metric-label">{{ title }}</div>
    <div class="metric-value">{{ value }}</div>
    <div v-if="trend" class="metric-trend" :class="trendUp ? 'up' : 'down'">
      <component :is="trendUp ? ArrowUpOutlined : ArrowDownOutlined" />
      <span>{{ trend }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'

defineProps<{
  title: string
  value: string | number
  trend?: string
  trendUp?: boolean
}>()
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.metric-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  padding: @spacing-xl;
  box-shadow: @shadow-sm;
  transition: all @transition-base;
  &:hover { box-shadow: @shadow-md; transform: translateY(-2px); }
}
.metric-label {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.metric-value {
  font-size: @font-size-3xl;
  font-weight: @font-weight-bold;
  color: @text-primary;
  line-height: 1;
}
.metric-trend {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  font-size: @font-size-xs;
  margin-top: @spacing-sm;
  &.up { color: @success; }
  &.down { color: @danger; }
}
</style>
