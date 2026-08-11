<template>
  <SectionCard title="强制性条款响应矩阵" flush>
    <div class="matrix-table__wrap">
      <table class="matrix-table">
        <thead>
          <tr>
            <th class="matrix-table__head">条款</th>
            <th v-for="d in documents" :key="d.id" class="matrix-table__head">{{ docLabel(d.id) }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, i) in matrix" :key="i">
            <td class="matrix-table__clause">{{ clauseTitle(i) }}</td>
            <td v-for="(cell, j) in row" :key="j" class="matrix-table__cell">
              <span class="matrix-cell" :class="cellClass(cell)">{{ cell }}</span>
            </td>
          </tr>
        </tbody>
      </table>
      <a-empty v-if="!matrix.length" description="暂无条款响应数据" />
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { CompareTask } from '@/types'

const props = defineProps<{ task: CompareTask }>()

const documents = computed(() => props.task.documents)
const matrix = computed(() => props.task.responseMatrix ?? [])

function docLabel(docId: string): string {
  const idx = documents.value.findIndex((d) => d.id === docId)
  return idx >= 0 ? String.fromCharCode(65 + idx) : docId
}

function clauseTitle(index: number): string {
  return props.task.matrixClauses?.[index] ?? `条款 ${index + 1}`
}

function cellClass(cell: string): string {
  if (cell === '√') return 'matrix-cell--ok'
  if (cell === '△') return 'matrix-cell--partial'
  return 'matrix-cell--missing'
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

  &--ok { color: @success; }
  &--partial { color: @warning; }
  &--missing { color: @danger; }
}
</style>
