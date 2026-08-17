<template>
  <a-modal
    :open="open"
    title="批量解析"
    width="640px"
    :footer="null"
    @cancel="emit('update:open', false)"
  >
    <a-empty v-if="!items.length" description="请先选择标准" image="simple" />
    <div v-else class="batch-parse">
      <div v-for="item in items" :key="item.id" class="batch-parse__row">
        <span class="batch-parse__name" :title="item.name">{{ item.name }}</span>
        <template v-if="item.status === 'parsing'">
          <a-spin size="small" />
          <a-tag color="blue">解析中</a-tag>
        </template>
        <template v-else-if="item.status === 'success'">
          <a-tag color="green">成功</a-tag>
          <AppButton variant="link" size="sm" @click="emit('view', item.id)">查看</AppButton>
        </template>
        <template v-else>
          <a-tag color="red">失败</a-tag>
          <AppButton variant="link" size="sm" @click="emit('retry', item.id)">重试</AppButton>
          <span class="batch-parse__error" :title="item.error">{{ item.error }}</span>
        </template>
      </div>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import type { StandardParseBatchItem } from '../types'

defineProps<{
  open: boolean
  items: StandardParseBatchItem[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'view': [id: string]
  'retry': [id: string]
}>()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.batch-parse {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;

  &__row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    padding: @spacing-xs @spacing-sm;
    background: @card-bg;
    border: 1px solid @border-color;
    border-radius: @radius-base;
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
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @danger;
  }
}
</style>
