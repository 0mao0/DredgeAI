<template>
  <SectionCard :title="title" flush>
    <div class="evidence-table__filters">
      <a-segmented v-model:value="severityFilter" :options="severityOptions" />
      <a-segmented v-if="!hideTypeFilter" v-model:value="typeFilter" :options="typeOptions" />
    </div>

    <a-table
      :columns="columns"
      :data-source="filtered"
      :loading="loading"
      row-key="id"
      size="small"
      :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
      :locale="{ emptyText: '暂无证据' }"
      :scroll="{ x: 1100 }"
      :custom-row="customRow"
      :row-class-name="() => (clickable ? 'evidence-table__row--clickable' : '')"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'severity'">
          <a-tag :color="severityColor(record.severity)">{{ severityText(record.severity) }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'type'">
          <a-tag :color="typeColor(record.type)">{{ typeText(record.type) }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'docIds'">
          {{ (record.docIds as string[]).map((id) => docLabel(documents ?? [], id)).join(' / ') }}
        </template>
        <template v-else-if="column.dataIndex === 'title'">
          <div class="evidence-table__title">{{ record.title }}</div>
          <div class="evidence-table__summary">{{ record.summary }}</div>
          <div v-if="metricHint(record)" class="evidence-table__metrics">{{ metricHint(record) }}</div>
        </template>
        <template v-else-if="column.dataIndex === 'confidence'">
          {{ record.confidence != null ? `${Math.round(record.confidence * 100)}%` : '—' }}
        </template>
        <template v-else-if="column.dataIndex === 'source'">
          <a-tag :color="record.source === 'ai' ? 'purple' : 'blue'">
            {{ record.source === 'ai' ? 'AI 分析' : '算法' }}
          </a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'action'">
          <a-button type="link" size="small" @click.stop="emit('jump', record)">查看</a-button>
          <a-button type="link" size="small" @click.stop="emit('trace', record)">溯源</a-button>
        </template>
      </template>
    </a-table>
  </SectionCard>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { evidenceMetricLines } from '../evidenceMetrics'
import { docLabel } from '../constants'
import type { CompareDocMeta, EvidenceItem, EvidenceType, RiskLevel } from '@/types'

const props = withDefaults(defineProps<{
  evidence: EvidenceItem[]
  documents?: CompareDocMeta[]
  loading?: boolean
  clickable?: boolean
  hideTypeFilter?: boolean
  title?: string
}>(), {
  title: '证据清单',
})

const emit = defineEmits<{ jump: [item: EvidenceItem], trace: [item: EvidenceItem] }>()

const severityFilter = ref<'all' | RiskLevel>('all')
const typeFilter = ref<'all' | EvidenceType>('all')

const severityOptions = [
  { label: '全部', value: 'all' },
  { label: '高', value: 'high' },
  { label: '中', value: 'mid' },
  { label: '低', value: 'low' },
]

const typeOptions = [
  { label: '全部', value: 'all' },
  { label: '雷同', value: 'similarity' },
  { label: '报价', value: 'price' },
  { label: '元数据', value: 'metadata' },
  { label: '条款', value: 'clause' },
  { label: '指标', value: 'indicator' },
]

const columns = computed(() => [
  ...(props.hideTypeFilter ? [] : [{ title: '类型', dataIndex: 'type', width: 90 }]),
  { title: '严重度', dataIndex: 'severity', width: 90 },
  { title: '涉及文档', dataIndex: 'docIds', width: 110 },
  { title: '证据', dataIndex: 'title' },
  { title: '置信度', dataIndex: 'confidence', width: 90 },
  { title: '来源', dataIndex: 'source', width: 90 },
  { title: '操作', dataIndex: 'action', width: 120 },
])

const filtered = computed(() =>
  props.evidence.filter((e) => {
    if (severityFilter.value !== 'all' && e.severity !== severityFilter.value) return false
    if (typeFilter.value !== 'all' && e.type !== typeFilter.value) return false
    return true
  }),
)

function customRow(record: EvidenceItem) {
  return {
    onClick: () => {
      if (props.clickable) emit('jump', record)
    },
  }
}

function severityColor(s: RiskLevel): string {
  return s === 'high' ? '#EF4444' : s === 'mid' ? '#F59E0B' : '#3B82F6'
}

function severityText(s: RiskLevel): string {
  return s === 'high' ? '高风险' : s === 'mid' ? '中风险' : '低风险'
}

function typeColor(t: EvidenceType): string {
  const map: Record<EvidenceType, string> = {
    similarity: 'red',
    price: 'orange',
    metadata: 'blue',
    clause: 'green',
    indicator: 'purple',
  }
  return map[t]
}

function typeText(t: EvidenceType): string {
  const map: Record<EvidenceType, string> = {
    similarity: '雷同',
    price: '报价',
    metadata: '元数据',
    clause: '条款',
    indicator: '指标',
  }
  return map[t]
}

function metricHint(record: EvidenceItem): string {
  return evidenceMetricLines(record).join(' · ')
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.evidence-table__filters {
  display: flex;
  gap: @spacing-base;
  padding: @spacing-base @spacing-xl 0;
  margin-bottom: @spacing-base;
  flex-wrap: wrap;
}

.evidence-table__title {
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
  text-align: left;
}

.evidence-table__summary {
  font-size: @font-size-xs;
  color: @text-tertiary;
  text-align: left;
  margin-top: 2px;
  line-height: 1.5;
}

.evidence-table__metrics {
  margin-top: 2px;
  font-size: @font-size-xs;
  color: @brand-primary;
  text-align: left;
}

:deep(.evidence-table__row--clickable) {
  cursor: pointer;
}
</style>
