<template>
  <div class="collusion-panel">
    <SectionCard title="文档元数据对比" flush>
      <div class="meta-table__wrap">
        <table class="meta-table">
          <thead>
            <tr>
              <th class="meta-table__head">字段</th>
              <th v-for="d in documents" :key="d.id" class="meta-table__head">{{ docLabel(d.id) }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="f in fields" :key="f.key">
              <td class="meta-table__label">{{ f.label }}</td>
              <td
                v-for="d in documents"
                :key="d.id"
                class="meta-table__cell"
                :class="{ 'meta-table__cell--dup': isDup(f.key, d.id) }"
              >
                {{ metaOf(d.id)?.[f.key] ?? '—' }}
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
      title="元数据证据"
      @jump="(ev) => emit('locate', ev)"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EvidenceTable from './EvidenceTable.vue'
import { mockDocMetaInfos } from '@shared/mock/data/compare'
import type { CompareDocMeta, CompareDocMetaInfo, EvidenceItem } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  evidence: EvidenceItem[]
}>()

const emit = defineEmits<{ locate: [item: EvidenceItem] }>()

const fields: { key: keyof Omit<CompareDocMetaInfo, 'docId' | 'createdAt'>, label: string }[] = [
  { key: 'author', label: '作者' },
  { key: 'creatorTool', label: '创建工具' },
  { key: 'producer', label: 'Producer' },
  { key: 'guid', label: 'GUID' },
  { key: 'ip', label: 'IP 地址' },
]

const metaEvidence = computed(() => props.evidence.filter((e) => e.type === 'metadata'))

function metaOf(docId: string): CompareDocMetaInfo | undefined {
  return mockDocMetaInfos.find((m) => m.docId === docId)
}

function isDup(key: (typeof fields)[number]['key'], docId: string): boolean {
  const v = metaOf(docId)?.[key]
  if (!v) return false
  return props.documents.filter((d) => metaOf(d.id)?.[key] === v).length > 1
}

function docLabel(docId: string): string {
  const idx = props.documents.findIndex((d) => d.id === docId)
  return idx >= 0 ? String.fromCharCode(65 + idx) : docId
}
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
