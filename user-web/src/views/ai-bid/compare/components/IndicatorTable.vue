<template>
  <SectionCard title="关键指标比选" flush>
    <div class="indicator-table__wrap">
      <a-table
        :columns="columns"
        :data-source="rows"
        row-key="id"
        size="small"
        :pagination="false"
        :locale="{ emptyText: '暂无指标数据（AI 指标抽取未完成或不可用）' }"
        :scroll="{ x: 900 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'indicator'">
            <span class="indicator-table__name">{{ record.name }}</span>
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <a-button type="link" size="small" @click="emit('trace', record.evidence)">溯源</a-button>
          </template>
          <template v-else>
            {{ record.cells[column.dataIndex] ?? '—' }}
          </template>
        </template>
      </a-table>
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { docLabel } from '../constants'
import type { CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  evidence: EvidenceItem[]
  documents: CompareDocMeta[]
}>()

const emit = defineEmits<{ trace: [item: EvidenceItem] }>()

const indicatorEvidence = computed(() => props.evidence.filter((e) => e.type === 'indicator'))

const columns = computed(() => [
  { title: '指标', dataIndex: 'indicator', width: 180 },
  ...props.documents.map((d) => ({
    title: docLabel(props.documents, d.id),
    dataIndex: d.id,
    width: 220,
  })),
  { title: '操作', dataIndex: 'action', width: 80 },
])

interface IndicatorRow {
  id: string
  name: string
  cells: Record<string, string>
  evidence: EvidenceItem
}

const rows = computed<IndicatorRow[]>(() => indicatorEvidence.value.map((ev) => ({
  id: ev.id,
  name: ev.title.replace(/^指标比选：/, ''),
  cells: summariesOf(ev).reduce<Record<string, string>>((acc, s) => {
    acc[s.docId] = replaceDocIds(s.summary, props.documents)
    return acc
  }, {}),
  evidence: ev,
})))

function summariesOf(ev: EvidenceItem): { docId: string, summary: string }[] {
  const raw = ev.metrics?.summaries
  if (Array.isArray(raw)) {
    const list = raw.filter((s): s is { docId: string, summary: string } =>
      !!s && typeof (s as { docId?: unknown }).docId === 'string' && typeof (s as { summary?: unknown }).summary === 'string')
    if (list.length) return list
  }
  // 兜底：从 description（docId: summary；…）解析
  return ev.summary
    .split('；')
    .map((part) => {
      const idx = part.indexOf(':')
      return idx > 0 ? { docId: part.slice(0, idx).trim(), summary: part.slice(idx + 1).trim() } : null
    })
    .filter((s): s is { docId: string, summary: string } => !!s)
}

function replaceDocIds(text: string, documents: CompareDocMeta[]): string {
  return text.replace(/\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, (id) => docLabel(documents, id))
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.indicator-table__wrap {
  padding: @spacing-base @spacing-xl @spacing-xl;
  overflow-x: auto;
}

.indicator-table__name {
  font-weight: @font-weight-medium;
  color: @text-primary;
  white-space: nowrap;
}
</style>
