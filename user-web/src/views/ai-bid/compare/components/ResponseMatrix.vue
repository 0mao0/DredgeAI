<template>
  <SectionCard title="强制性条款响应矩阵" flush>
    <div class="matrix-table__wrap">
      <table class="matrix-table">
        <thead>
          <tr>
            <th class="matrix-table__head">条款</th>
            <th v-for="d in bidDocs" :key="d.id" class="matrix-table__head">{{ docLabel(bidDocs, d.id) }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.clauseId">
            <td class="matrix-table__clause">{{ row.text }}</td>
            <td v-for="d in bidDocs" :key="d.id" class="matrix-table__cell">
              <a-button
                v-if="cellOf(row, d.id)"
                type="link"
                size="small"
                class="matrix-cell"
                :class="cellClass(cellOf(row, d.id)!)"
                @click="emit('trace', cellOf(row, d.id)!)"
              >
                {{ cellText(cellOf(row, d.id)!) }}
              </a-button>
              <span v-else class="matrix-cell matrix-cell--ok">响应</span>
            </td>
          </tr>
        </tbody>
      </table>
      <a-empty v-if="!rows.length" description="暂无条款响应数据（需要招标文件并完成条款确认后由 AI 判定）" />
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { docLabel } from '../constants'
import type { CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  evidence: EvidenceItem[]
}>()

const emit = defineEmits<{ trace: [item: EvidenceItem] }>()

const bidDocs = computed(() => props.documents.filter((d) => d.role !== 'tender'))
const clauseEvidence = computed(() => props.evidence.filter((e) => e.type === 'clause'))

interface ClauseRow {
  clauseId: string
  text: string
  items: EvidenceItem[]
}

const rows = computed<ClauseRow[]>(() => {
  const map = new Map<string, ClauseRow>()
  for (const ev of clauseEvidence.value) {
    const clauseId = String(ev.metrics?.clauseId ?? ev.title)
    const row = map.get(clauseId) ?? {
      clauseId,
      text: String(ev.metrics?.clauseText ?? ev.title.replace(/^条款未实质响应（[^）]*）：/, '')),
      items: [],
    }
    row.items.push(ev)
    map.set(clauseId, row)
  }
  return [...map.values()]
})

function cellOf(row: ClauseRow, docId: string): EvidenceItem | undefined {
  return row.items.find((ev) => ev.docIds.includes(docId))
}

function cellText(ev: EvidenceItem): string {
  return ev.metrics?.status === 'partial' ? '部分响应' : '未响应'
}

function cellClass(ev: EvidenceItem): string {
  return ev.metrics?.status === 'partial' ? 'matrix-cell--partial' : 'matrix-cell--missing'
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.matrix-table__wrap {
  padding: @spacing-base @spacing-xl @spacing-xl;
  overflow-x: auto;
}

.matrix-table {
  width: 100%;
  border-collapse: collapse;

  th, td {
    padding: @spacing-sm @spacing-md;
    text-align: center;
    border-bottom: 1px solid @divider-color;
    font-size: @font-size-sm;
  }
}

.matrix-table__head {
  color: @text-secondary;
  font-weight: @font-weight-medium;
}

.matrix-table__clause {
  text-align: left !important;
  color: @text-primary;
  font-weight: @font-weight-medium;
}

.matrix-cell {
  font-weight: @font-weight-semibold;
  padding: 0;
  height: auto;

  &--ok { color: @success; }
  &--partial { color: @warning; }
  &--missing { color: @danger; }
}
</style>
