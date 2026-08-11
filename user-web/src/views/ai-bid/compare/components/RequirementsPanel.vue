<template>
  <SectionCard flush class="req-panel">
    <template #title>
      <span>招标要求 <span class="req-panel__count">{{ local.length }}</span></span>
    </template>
    <template #extra>
      <a-button size="small" @click="addRow">
        <PlusOutlined />添加要求
      </a-button>
    </template>

    <div class="req-panel__list">
      <div v-for="(c, i) in local" :key="c.id" class="req-row">
        <a-switch v-model:checked="c.mandatory" size="small" />
        <div class="req-row__body">
          <a-input v-model:value="c.title" size="small" placeholder="要求标题" />
          <a-textarea v-model:value="c.content" :rows="2" placeholder="要求内容" />
        </div>
        <a-tag class="req-row__cat">{{ c.category }}</a-tag>
        <a-button type="text" size="small" danger @click="local.splice(i, 1)">
          <DeleteOutlined />
        </a-button>
      </div>
      <a-empty v-if="!local.length" description="未提取到要求，可手动添加" />
    </div>

    <div class="req-panel__footer">
      <span class="req-panel__hint">提取自招标文件，修改后保存生效</span>
      <a-button type="primary" size="small" :loading="saving" :disabled="!local.length" @click="emit('save', local)">
        保存要求
      </a-button>
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { ClauseItem } from '@/types'

const props = defineProps<{
  clauses: ClauseItem[]
  saving?: boolean
}>()

const emit = defineEmits<{ save: [list: ClauseItem[]] }>()

const local = ref<ClauseItem[]>([])

watch(() => props.clauses, (c) => {
  local.value = c.map((x) => ({ ...x }))
}, { immediate: true })

function addRow(): void {
  local.value.push({
    id: `tmp-${Date.now()}`,
    title: '',
    content: '',
    category: '通用',
    mandatory: true,
    source: 'user_added',
  })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.req-panel {
  &__count {
    font-size: @font-size-xs;
    color: @text-tertiary;
    font-weight: @font-weight-regular;
  }

  &__list {
    padding: @spacing-base @spacing-xl;
    display: flex;
    flex-direction: column;
    gap: @spacing-sm;
  }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: @spacing-md @spacing-xl;
    border-top: 1px solid @divider-color;
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.req-row {
  display: flex;
  align-items: flex-start;
  gap: @spacing-sm;
  padding: @spacing-sm;
  border: 1px solid @border-color;
  border-radius: @radius-base;

  &__body {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: @spacing-xs;
  }

  &__cat {
    flex-shrink: 0;
    margin-top: 4px;
  }
}
</style>
