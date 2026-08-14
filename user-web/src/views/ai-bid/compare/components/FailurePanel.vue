<template>
  <div class="failure-panel">
    <a-result status="error" title="分析失败" :sub-title="reason" />

    <div class="failure-panel__body">
      <SectionCard title="失败文档" flush>
        <div v-if="failedDocs.length" class="failure-list">
          <div v-for="d in failedDocs" :key="d.id" class="failure-list__row">
            <span class="failure-list__label">{{ docLabel(task.documents, d.id) }}</span>
            <span class="failure-list__name" :title="d.fileName">{{ d.fileName }}</span>
            <span class="failure-list__error" :title="d.failReason">{{ d.failReason }}</span>
            <a-button type="link" size="small" @click="emit('reparseDoc', d.id)">重新解析</a-button>
          </div>
        </div>
        <a-empty v-else description="无失败文档（算法服务失败）" />
      </SectionCard>

      <div v-if="parsedBidCount < 2" class="failure-panel__hint">
        <ExclamationCircleOutlined />可用标书不足 2 份，请先重新解析失败文档后再重新对比
      </div>

      <div class="failure-panel__actions">
        <a-button
          v-if="failedDocs.length"
          size="large"
          :loading="reparseAllLoading"
          @click="emit('reparseAll')"
        >
          重新解析失败文档
        </a-button>
        <a-button
          type="primary"
          size="large"
          :disabled="parsedBidCount < 2"
          :loading="retryingCompare"
          @click="emit('retryCompare')"
        >
          重新对比
        </a-button>
        <a-button size="large" @click="emit('back')">返回上传</a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { ExclamationCircleOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { docLabel } from '../constants'
import type { CompareTask } from '@/types'

const props = defineProps<{
  task: CompareTask
  reparseAllLoading: boolean
  retryingCompare: boolean
}>()

const emit = defineEmits<{
  reparseDoc: [docId: string]
  reparseAll: []
  retryCompare: []
  back: []
}>()

const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))
const parsedBidCount = computed(() =>
  props.task.documents.filter((d) => d.role !== 'tender' && d.parseStatus === 'done').length,
)
const reason = computed(() =>
  props.task.failReason ?? props.task.progress.message ?? '任务处理失败，请重试或返回上传',
)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.failure-panel {
  height: 100%;
  min-height: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
  padding: @spacing-md @spacing-base @spacing-xl;
}

.failure-panel__body {
  max-width: 640px;
  width: 100%;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
}

.failure-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
  padding: @spacing-base @spacing-xl @spacing-xl;

  &__row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    font-size: @font-size-sm;
  }

  &__label {
    flex-shrink: 0;
    font-weight: @font-weight-semibold;
    color: @text-primary;
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }

  &__error {
    max-width: 260px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @danger;
  }
}

.failure-panel__hint {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border: 1px solid @warning;
  border-radius: @radius-base;
  background: color-mix(in srgb, @warning 8%, @card-bg);
  font-size: @font-size-xs;
  color: @text-secondary;
}

.failure-panel__actions {
  display: flex;
  justify-content: flex-end;
  gap: @spacing-sm;
}
</style>
