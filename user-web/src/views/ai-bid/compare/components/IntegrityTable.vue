<template>
  <SectionCard title="文档列表" flush>
    <a-table
      :columns="columns"
      :data-source="documents"
      :loading="loading"
      row-key="id"
      size="small"
      :pagination="false"
      :locale="{ emptyText: '暂无文档' }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'fileName'">
          <span class="doc-table__name" :title="record.fileName">{{ record.fileName }}</span>
        </template>
        <template v-else-if="column.dataIndex === 'parseStatus'">
          <a-tag :color="statusColor(record.parseStatus)">{{ statusText(record.parseStatus) }}</a-tag>
        </template>
        <template v-else-if="column.dataIndex === 'ocr'">
          <a-tag v-if="record.isLowConfidenceOcr" color="orange">扫描件</a-tag>
          <span v-else class="doc-table__ok">正常</span>
        </template>
        <template v-else-if="column.dataIndex === 'action'">
          <AppButton
            variant="link"
            size="sm"
            :disabled="record.parseStatus !== 'done'"
            @click="emit('jump', record.id, 1)"
          >
            定位
          </AppButton>
        </template>
      </template>
    </a-table>
    <div v-if="hasLowConfidence" class="doc-table__hint">
      含扫描件文档，其查重结果可能存在偏差
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { AppButton } from '@shared/web'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { CompareDocMeta } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  loading?: boolean
}>()

const emit = defineEmits<{ jump: [docId: string, page: number] }>()

const columns = [
  { title: '文件名', dataIndex: 'fileName' },
  { title: '页数', dataIndex: 'pages', width: 70 },
  { title: '状态', dataIndex: 'parseStatus', width: 90 },
  { title: 'OCR 质量', dataIndex: 'ocr', width: 100 },
  { title: '操作', dataIndex: 'action', width: 70 },
]

const hasLowConfidence = computed(() => props.documents.some((d) => d.isLowConfidenceOcr))

function statusColor(s: CompareDocMeta['parseStatus']): string {
  const map: Record<CompareDocMeta['parseStatus'], string> = {
    pending: 'default',
    parsing: 'blue',
    done: 'green',
    failed: 'red',
  }
  return map[s]
}

function statusText(s: CompareDocMeta['parseStatus']): string {
  const map: Record<CompareDocMeta['parseStatus'], string> = {
    pending: '待解析',
    parsing: '解析中',
    done: '已解析',
    failed: '失败',
  }
  return map[s]
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.doc-table__name {
  display: inline-block;
  max-width: 260px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;
}

.doc-table__ok {
  font-size: @font-size-xs;
  color: @text-tertiary;
}

.doc-table__hint {
  padding: @spacing-sm @spacing-xl @spacing-base;
  font-size: @font-size-xs;
  color: @warning;
}
</style>
