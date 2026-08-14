<template>
  <div class="metric-row">
    <MetricCard title="标书份数" :value="docCount" icon="FileOutlined" color="var(--color-brand)" />
    <MetricCard title="高风险" :value="counts.high" icon="WarningOutlined" color="var(--color-danger)" />
    <MetricCard title="中风险" :value="counts.mid" icon="ExclamationCircleOutlined" color="var(--color-warning)" />
    <MetricCard title="低风险" :value="counts.low" icon="InfoCircleOutlined" color="var(--color-info)" />
    <MetricCard title="条款不响应" :value="counts.clauseMissing" icon="FileSearchOutlined" color="var(--color-accent)" />
    <MetricCard title="报价异常" :value="counts.price" icon="MoneyCollectOutlined" color="var(--color-warning)" />
    <MetricCard title="元数据痕迹" :value="counts.meta" icon="SafetyCertificateOutlined" color="var(--color-info)" />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import type { EvidenceItem } from '@/types'

const props = defineProps<{
  evidence: EvidenceItem[]
  docCount: number
}>()

const counts = computed(() => {
  return {
    high: props.evidence.filter((e) => e.severity === 'high').length,
    mid: props.evidence.filter((e) => e.severity === 'mid').length,
    low: props.evidence.filter((e) => e.severity === 'low').length,
    clauseMissing: props.evidence.filter((e) => e.type === 'clause' && e.severity === 'high').length,
    price: props.evidence.filter((e) => e.type === 'price').length,
    meta: props.evidence.filter((e) => e.type === 'metadata').length,
  }
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.metric-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: @spacing-md;
}

@media (max-width: 991px) {
  .metric-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
</style>
