<template>
  <div class="bid-panel">
    <a-tabs v-model:active-key="activeTab">
      <a-tab-pane key="risk" tab="风险清单">
        <template #extra>
          <a-button type="link" size="small">
            <DownloadOutlined /> 导出报告
          </a-button>
        </template>
        <div class="risk-summary">
          <div v-for="level in riskSummary" :key="level.label" class="risk-stat">
            <div class="risk-stat-num" :class="`risk-stat-num--${level.key}`">{{ level.count }}</div>
            <div class="risk-stat-label">{{ level.label }}</div>
          </div>
        </div>
        <transition-group name="risk-stagger" tag="div" class="risk-list">
          <div
            v-for="(risk, i) in risks"
            :key="risk.id"
            class="risk-card"
            :class="`risk-card--${risk.level}`"
            :style="{ transitionDelay: `${i * 0.04}s` }"
          >
            <div class="risk-card-header">
              <StatusTag :status="risk.level" />
              <span class="risk-source">{{ risk.source }}</span>
            </div>
            <div class="risk-content">{{ risk.content }}</div>
            <div v-if="risk.suggestion" class="risk-suggestion">
              <BulbOutlined />
              <span>{{ risk.suggestion }}</span>
            </div>
          </div>
        </transition-group>
      </a-tab-pane>
      <a-tab-pane key="chat" tab="AI 对话">
        <AIChat :messages="chatMessages" empty-text="暂无对话，上传文档后向 AI 追问标书细节" @send="$emit('chatSend', $event)" />
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { DownloadOutlined, BulbOutlined } from '@ant-design/icons-vue'
import StatusTag from '@/components/StatusTag.vue'
import AIChat from '@shared/web/components/AIChat.vue'
import type { RiskItem } from '@/types'
import type { ChatMessage } from '@shared/core/types/chat'

defineProps<{
  risks: RiskItem[]
  riskSummary: { key: string, label: string, count: number }[]
  chatMessages: ChatMessage[]
}>()

defineEmits<{ chatSend: [text: string] }>()

const activeTab = ref<'risk' | 'chat'>('risk')
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.bid-panel {
  height: 100%;
  display: flex;
  flex-direction: column;

  :deep(.ant-tabs) { flex: 1; min-height: 0; display: flex; flex-direction: column; }
  :deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; flex-shrink: 0; }
  :deep(.ant-tabs-content-holder) { flex: 1; min-height: 0; display: flex; flex-direction: column; overflow: hidden; }
  :deep(.ant-tabs-content) { flex: 1; min-height: 0; height: 100%; }
  :deep(.ant-tabs-tabpane) { height: 100%; }
}

/* —— 风险 —— */
.risk-summary {
  display: flex; justify-content: space-around;
  padding: @spacing-md 0;
}
.risk-stat { text-align: center; }
.risk-stat-num {
  font-size: 28px; font-weight: @font-weight-bold; line-height: 1;
  margin-bottom: 4px;
  &--high { color: @danger; }
  &--mid { color: @warning; }
  &--low { color: @info; }
}
.risk-stat-label { font-size: @font-size-xs; color: @text-secondary; }

.risk-list { display: flex; flex-direction: column; gap: @spacing-md; }
.risk-card {
  background: @card-bg;
  border-radius: @radius-base;
  border: 1px solid @border-color;
  border-left: 3px solid;
  padding: @spacing-md;
  transition: all @transition-base;
  &:hover {
    box-shadow: @shadow-md;
    transform: translateX(2px);
  }
  &--高风险 { border-left-color: @danger; }
  &--中风险 { border-left-color: @warning; }
  &--低风险 { border-left-color: @info; }
}
.risk-card-header {
  display: flex; align-items: center; justify-content: space-between;
  margin-bottom: @spacing-sm;
}
.risk-source { font-size: @font-size-xs; color: @text-tertiary; }
.risk-content { font-size: @font-size-sm; color: @text-primary; line-height: 1.5; margin-bottom: @spacing-sm; }
.risk-suggestion {
  display: flex; gap: @spacing-xs; align-items: flex-start;
  font-size: @font-size-xs; color: @text-secondary;
  background: @content-bg;
  padding: @spacing-sm; border-radius: @radius-sm;
}

/* —— risk stagger —— */
.risk-stagger-enter-active { transition: all 0.3s ease; }
.risk-stagger-leave-active { transition: all 0.2s ease; }
.risk-stagger-enter-from { opacity: 0; transform: translateX(-12px); }
.risk-stagger-leave-to { opacity: 0; transform: translateX(12px); }
</style>
