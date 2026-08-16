<template>
  <div class="standard-property">
    <DataSkeleton v-if="loading" />
    <EmptyState v-else-if="!property" title="请先选择标准" />
    <EmptyState v-else-if="error" type="error" title="加载失败" :description="error" />
    <template v-else>
      <a-tabs v-model:active-key="activeTab" class="standard-property__tabs">
        <a-tab-pane key="properties" tab="属性">
          <div class="standard-scroll">
            <a-form :model="form" layout="vertical">
              <a-form-item label="名称" required><a-input v-model:value="form.name" /></a-form-item>
              <a-form-item label="编号" required><a-input v-model:value="form.code" /></a-form-item>
              <a-form-item label="行业"><a-select v-model:value="form.industry" :options="industryOptions" /></a-form-item>
              <a-form-item label="性质"><a-select v-model:value="form.nature" :options="natureOptions" /></a-form-item>
              <a-form-item label="级别"><a-select v-model:value="form.level" :options="levelOptions" /></a-form-item>
              <a-form-item label="状态"><a-select v-model:value="form.status" :options="statusOptions" /></a-form-item>
              <a-form-item label="发布部门"><a-select v-model:value="form.issuer" :options="issuerOptions" /></a-form-item>
              <a-form-item label="发布时间(年)"><a-input-number v-model:value="form.publishYear" :min="1900" :max="2030" style="width:100%" /></a-form-item>
              <a-form-item label="上级目录"><a-select v-model:value="form.parentId" :options="parentOptions" /></a-form-item>
              <a-form-item label="简介"><a-textarea v-model:value="form.description" :rows="3" /></a-form-item>
              <a-form-item><a-button type="primary" :loading="submitting" @click="handleSubmit">提交</a-button></a-form-item>
            </a-form>
          </div>
        </a-tab-pane>

        <a-tab-pane key="ai" tab="AI对话" force-render>
          <AIChat :messages="chatMessagesAIChat" empty-text="你好！可以针对所选标准向我提问。" @send="handleChat" />
        </a-tab-pane>
      </a-tabs>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { message } from 'ant-design-vue'
import DataSkeleton from '@shared/web/components/DataSkeleton.vue'
import { EmptyState, AIChat } from '@shared/web'

import type { ChatMessage } from '@shared/core/types/chat'
import type { StandardProperty } from '@/types'

interface InternalMessage { role: 'user' | 'ai', content: string }

const props = defineProps<{
  property: StandardProperty | null
  loading: boolean
  error: string | null
  submitting: boolean
}>()

const emit = defineEmits<{ submit: [data: Partial<StandardProperty>] }>()

const activeTab = ref('properties')

const form = ref<Partial<StandardProperty>>({ name: '', code: '' })
watch(() => props.property, (p) => { if (p) form.value = { ...p } }, { immediate: true })

function handleSubmit(): void {
  if (!form.value.name?.trim() || !form.value.code?.trim()) { message.warning('请填写名称和编号'); return }
  emit('submit', { ...form.value })
}

const chatMessages = ref<InternalMessage[]>([
  { role: 'ai', content: '你好！可以针对所选标准向我提问。' },
])

const chatMessagesAIChat = computed<ChatMessage[]>(() =>
  chatMessages.value.map((m) => ({
    role: m.role === 'ai' ? 'assistant' : 'user',
    content: m.content,
  })),
)

function handleChat(text: string): void {
  chatMessages.value.push({ role: 'user', content: text })
  setTimeout(() => {
    chatMessages.value.push({ role: 'ai', content: '已收到您的问题。请查阅规范原文以获取最准确的信息。' })
  }, 600)
}

const industryOptions = [
  { value: '水利', label: '水利' },
  { value: '建筑', label: '建筑' },
  { value: '交通', label: '交通' },
  { value: '环保', label: '环保' },
  { value: '能源', label: '能源' },
]
const natureOptions = [{ value: '强制性标准', label: '强制性标准' }, { value: '推荐性标准', label: '推荐性标准' }]
const levelOptions = [
  { value: '国家标准', label: '国家标准' },
  { value: '行业标准', label: '行业标准' },
  { value: '地方标准', label: '地方标准' },
  { value: '团体标准', label: '团体标准' },
]
const statusOptions = [{ value: '现行', label: '现行' }, { value: '废止', label: '废止' }]
const issuerOptions = [
  { value: '国务院', label: '国务院' },
  { value: '全国人大常委会', label: '全国人大常委会' },
  { value: '水利部', label: '水利部' },
  { value: '住建部', label: '住建部' },
  { value: '生态环境部', label: '生态环境部' },
]
const parentOptions = [
  { value: '国家标准', label: '国家标准' },
  { value: '行业标准', label: '行业标准' },
  { value: '地方标准', label: '地方标准' },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.standard-property {
  height: 100%;
  display: flex;
  flex-direction: column;

  :deep(.ant-tabs) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }
  :deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; flex-shrink: 0; }
  :deep(.ant-tabs-content-holder) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }
  :deep(.ant-tabs-content) { flex: 1; min-height: 0; height: 100%; }
  :deep(.ant-tabs-tabpane) { height: 100%; }
}

.standard-scroll { height: 100%; overflow-y: auto; padding: 0 @spacing-md @spacing-md 0; }
</style>
