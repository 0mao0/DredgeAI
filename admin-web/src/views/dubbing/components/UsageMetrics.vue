<template>
  <div class="metrics-row">
    <MetricCard title="总任务数" :value="summary ? summary.totalTasks : '-'" suffix="个" icon="SoundOutlined" :color="brandColor" />
    <MetricCard title="Token 总消耗" :value="summary ? summary.totalTokens : '-'" suffix="tokens" icon="DatabaseOutlined" :color="accentColor" />
    <MetricCard title="活跃用户数" :value="summary ? summary.totalUsers : '-'" suffix="人" icon="TeamOutlined" :color="successColor" />
    <MetricCard title="总音频时长" :value="summary ? summary.totalAudioSec : '-'" suffix="秒" icon="ClockCircleOutlined" :color="warningColor" />
  </div>
</template>

<script setup lang="ts">
import MetricCard from '@shared/web/components/MetricCard.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import type { DubbingUsageSummary } from '@/types'

defineProps<{ summary: DubbingUsageSummary | null, loading: boolean }>()

const brandColor = useCssVar('--color-brand')
const accentColor = useCssVar('--color-accent')
const successColor = useCssVar('--color-success')
const warningColor = useCssVar('--color-warning')
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.metrics-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: @spacing-lg;
  margin-bottom: @spacing-xl;
}

@media (max-width: 1200px) {
  .metrics-row { grid-template-columns: repeat(2, 1fr); }
}
@media (max-width: 576px) {
  .metrics-row { grid-template-columns: 1fr; }
}
</style>
