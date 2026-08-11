<template>
  <SectionCard title="关键指标比选" flush>
    <div class="metrics-table__wrap">
      <table class="metrics-table">
        <thead>
          <tr>
            <th class="metrics-table__head">指标</th>
            <th v-for="d in documents" :key="d.id" class="metrics-table__head">{{ docLabel(d.id) }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.label">
            <td class="metrics-table__label">{{ row.label }}</td>
            <td v-for="(v, i) in row.values.slice(0, documents.length)" :key="i" class="metrics-table__value">
              {{ v }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { CompareDocMeta } from '@/types'

const props = defineProps<{ documents: CompareDocMeta[] }>()

const documents = computed(() => props.documents)

const rows = [
  { label: '报价（万元）', values: ['28500', '28300', '29100', '27800', '28600'] },
  { label: '工期（月）', values: ['18', '18', '17', '18', '18'] },
  { label: '资质等级', values: ['一级', '一级', '一级', '二级', '一级'] },
  { label: '质量目标', values: ['优良', '优良', '合格', '优良', '优良'] },
]

function docLabel(docId: string): string {
  const idx = documents.value.findIndex((d) => d.id === docId)
  return idx >= 0 ? String.fromCharCode(65 + idx) : docId
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.metrics-table__wrap {
  padding: @spacing-base @spacing-xl @spacing-xl;
  overflow-x: auto;
}

.metrics-table {
  width: 100%;
  border-collapse: collapse;

  th, td {
    padding: @spacing-sm @spacing-md;
    text-align: center;
    border-bottom: 1px solid @divider-color;
    font-size: @font-size-sm;
  }
}

.metrics-table__head {
  color: @text-secondary;
  font-weight: @font-weight-medium;
}

.metrics-table__label {
  text-align: left !important;
  color: @text-primary;
  font-weight: @font-weight-medium;
  white-space: nowrap;
}

.metrics-table__value {
  color: @text-secondary;
}
</style>
