<template>
  <div class="collusion-panel">
    <SectionCard v-if="metaRows.length" title="文档属性信息对比" flush>
      <div class="meta-table__wrap">
        <table class="meta-table">
          <thead>
            <tr>
              <th class="meta-table__head">字段</th>
              <th class="meta-table__head">一致值</th>
              <th v-for="d in documents" :key="d.id" class="meta-table__head">
                <DocBadge :label="docLabel(documents, d.id)" />
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in metaRows" :key="`${row.field}-${row.value}`">
              <td class="meta-table__label">{{ row.label }}</td>
              <td class="meta-table__value" :title="row.title">{{ row.value }}</td>
              <td v-for="d in documents" :key="d.id" class="meta-table__cell" :class="{ 'meta-table__cell--dup': row.docIds.includes(d.id) }">
                {{ row.docIds.includes(d.id) ? '✓' : '—' }}
              </td>
            </tr>
          </tbody>
        </table>
        <div class="meta-table__hint">标红单元格表示该字段在多份标书中一致，存在同源编制嫌疑</div>
      </div>
    </SectionCard>

    <EvidenceTable
      :evidence="metaEvidence"
      :documents="documents"
      hide-type-filter
      clickable
      title="属性信息证据"
      @jump="(ev) => emit('locate', ev)"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EvidenceTable from './EvidenceTable.vue'
import DocBadge from './DocBadge.vue'
import { docLabel } from '../constants'
import type { CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  evidence: EvidenceItem[]
}>()

const emit = defineEmits<{ locate: [item: EvidenceItem] }>()

const FIELD_LABELS: Record<string, string> = {
  author: '作者',
  createdAt: '创建时间',
  creatorTool: '创建工具',
}

const metaEvidence = computed(() => props.evidence.filter((e) => e.type === 'metadata'))

/** 后端 metadata 证据的 metrics 形如 { field, value }，逐条渲染成对比行。 */
const metaRows = computed(() =>
  metaEvidence.value.flatMap((ev) => {
    const m = ev.metrics
    if (!m || typeof m.field !== 'string' || typeof m.value !== 'string') return []
    return [{
      field: m.field,
      label: FIELD_LABELS[m.field] ?? m.field,
      value: m.value,
      docIds: ev.docIds,
      title: ev.title,
    }]
  }),
)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.collusion-panel {
  display: flex;
  flex-direction: column;
  gap: @spacing-xl;
}

.meta-table__wrap {
  padding: @spacing-base @spacing-xl @spacing-xl;
  overflow-x: auto;
}

.meta-table {
  width: 100%;
  border-collapse: collapse;

  th, td {
    padding: @spacing-sm @spacing-md;
    text-align: center;
    border-bottom: 1px solid @divider-color;
    font-size: @font-size-sm;
  }
}

.meta-table__head {
  color: @text-secondary;
  font-weight: @font-weight-medium;
}

.meta-table__label {
  text-align: left !important;
  color: @text-primary;
  font-weight: @font-weight-medium;
  white-space: nowrap;
}

.meta-table__value {
  color: @text-primary;
  font-weight: @font-weight-medium;
  max-width: 280px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.meta-table__cell {
  color: @text-secondary;
  font-variant-numeric: tabular-nums;

  &--dup {
    color: @danger;
    font-weight: @font-weight-medium;
    background: color-mix(in srgb, @danger 8%, transparent);
  }
}

.meta-table__hint {
  margin-top: @spacing-sm;
  font-size: @font-size-xs;
  color: @text-tertiary;
  text-align: left;
}
</style>
