<template>
  <div class="passage-table">
    <a-table
      size="small"
      :columns="columns"
      :data-source="rows"
      :pagination="false"
      row-key="__index"
      :scroll="{ x: 780 }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'text'">
          <a-tooltip :title="record.text" placement="topLeft">
            <span class="passage-table__text">{{ record.text }}</span>
          </a-tooltip>
        </template>
        <template v-else-if="column.key === 'location'">
          <span class="passage-table__loc">
            <DocBadge :label="docLabel(documents, evidence.docIds[0])" />
            <span class="passage-table__pages">{{ joinPages(record.docA.pages) }}</span>
            <span class="passage-table__arrow">↔</span>
            <DocBadge :label="docLabel(documents, evidence.docIds[1])" />
            <span class="passage-table__pages">{{ joinPages(record.docB.pages) }}</span>
          </span>
        </template>
        <template v-else-if="column.key === 'tender'">
          <a-tag v-if="record.tenderResponse === true" color="green">招标响应</a-tag>
          <a-tag v-else-if="record.tenderResponse === false" color="red">雷同候选</a-tag>
          <span v-else class="passage-table__unknown">—</span>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import DocBadge from './DocBadge.vue'
import { docLabel } from '../constants'
import type { CompareDocMeta, EvidenceItem } from '@/types'

interface PassageItem {
  text: string
  length: number
  docA: { blockIds: string[], pages: number[] }
  docB: { blockIds: string[], pages: number[] }
  tenderResponse?: boolean | null
  tenderRatio?: number
}

const props = defineProps<{
  evidence: EvidenceItem
  documents: CompareDocMeta[]
}>()

const columns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 56 },
  { title: '雷同片段', dataIndex: 'text', key: 'text' },
  { title: '字数', dataIndex: 'length', key: 'length', width: 72 },
  { title: '位置', key: 'location', width: 220 },
  { title: '判定', key: 'tender', width: 110 },
]

const rows = computed(() =>
  [...((props.evidence.metrics?.passages as PassageItem[] | undefined) ?? [])]
    .sort((a, b) => b.length - a.length)
    .map((p, i) => ({
      ...p,
      __index: i,
      index: i + 1,
    })),
)

function joinPages(pages: number[] | undefined): string {
  const list = pages ?? []
  return list.length ? `第 ${list.join('/')} 页` : ''
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.passage-table {
  padding: @spacing-xs @spacing-xl @spacing-md;

  &__text {
    display: block;
    max-width: 260px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @text-primary;
  }

  &__loc {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-size: @font-size-xs;
    color: @text-secondary;
  }

  &__pages {
    white-space: nowrap;
  }

  &__arrow {
    color: @text-tertiary;
  }

  &__unknown {
    color: @text-tertiary;
    font-size: @font-size-xs;
  }
}
</style>
