<template>
  <div class="metric-card" :style="{ borderTopColor: color || 'transparent' }">
    <div class="metric-top">
      <span class="metric-label">{{ title }}</span>
      <component :is="iconComp" v-if="iconComp" class="metric-icon" :style="{ color }" />
    </div>
    <div class="metric-value">
      {{ displayValue }}<span v-if="suffix" class="metric-suffix">{{ suffix }}</span>
    </div>
    <div v-if="trend !== undefined" class="metric-trend" :class="trendUp ? 'up' : 'down'">
      <ArrowUpOutlined v-if="trendUp" />
      <ArrowDownOutlined v-else />
      <span>{{ Math.abs(trend) }}%</span>
      <span class="trend-label">较上月</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import { useCountUp } from '../composables/useCountUp'

const props = defineProps<{
  title: string
  value: string | number
  suffix?: string
  trend?: number
  trendUp?: boolean
  icon?: string
  color?: string
}>()

const iconComp = computed(() => {
  if (!props.icon) return null
  return (Icons as Record<string, unknown>)[props.icon]
})

// 数值型 value 启用 CountUp 动画，字符串原样展示
const isNumeric = computed(() => typeof props.value === 'number' && Number.isFinite(props.value))
const { display } = useCountUp(() => (isNumeric.value ? (props.value as number) : 0))
const displayValue = computed(() => (isNumeric.value ? display.value : props.value))
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.metric-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  border-top: 3px solid transparent;
  padding: @spacing-xl;
  box-shadow: @shadow-sm;
  transition: transform @transition-base, box-shadow @transition-base;
  &:hover {
    box-shadow: @shadow-md;
    transform: translateY(-2px);
    .metric-icon { transform: rotate(-8deg) scale(1.12); opacity: 1; }
  }
}
.metric-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-sm;
}
.metric-label {
  font-size: @font-size-sm;
  color: @text-secondary;
}
.metric-icon {
  font-size: 20px;
  opacity: 0.7;
  transition: transform @transition-base, opacity @transition-base;
}
.metric-value {
  font-size: @font-size-3xl;
  font-weight: @font-weight-bold;
  color: @text-primary;
  line-height: 1.1;
}
.metric-suffix {
  font-size: @font-size-base;
  font-weight: @font-weight-regular;
  color: @text-secondary;
  margin-left: @spacing-xs;
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
.trend-label {
  color: @text-tertiary;
}
</style>
