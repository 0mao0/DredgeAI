<template>
  <div class="metric-row">
    <MetricCard title="标书份数" :value="docCount" icon="FileOutlined" color="var(--color-brand)" />
    <MetricCard title="高风险证据" :value="counts.high" icon="WarningOutlined" color="var(--color-danger)" />
    <MetricCard title="中风险证据" :value="counts.mid" icon="ExclamationCircleOutlined" color="var(--color-warning)" />
    <MetricCard title="条款不响应" :value="counts.clauseMissing" icon="FileSearchOutlined" color="var(--color-accent)" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import type { EvidenceItem } from '@/types'

const props = defineProps<{
  evidence: EvidenceItem[]
  docCount: number
  riskSummary?: { high: number, mid: number, low: number, clauseMissing: number }
}>()

const counts = computed(() => {
  if (props.riskSummary) return props.riskSummary
  return {
    high: props.evidence.filter((e) => e.severity === 'high').length,
    mid: props.evidence.filter((e) => e.severity === 'mid').length,
    low: props.evidence.filter((e) => e.severity === 'low').length,
    clauseMissing: props.evidence.filter((e) => e.type === 'clause' && e.severity === 'high').length,
  }
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.metric-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: @spacing-xl;
}

@media (max-width: 991px) {
  .metric-row { grid-template-columns: repeat(2, 1fr); }
}
</style>
