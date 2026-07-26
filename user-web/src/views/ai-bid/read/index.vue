<template>
  <div class="bid-read">
    <SectionCard title="审标进度" class="steps-card">
      <a-steps v-model:current="currentStep" size="small" :percent="currentPercent">
        <a-step
          v-for="(step, i) in steps"
          :key="i"
          :title="step.title"
          :description="step.progress != null ? `${step.progress}%` : step.description"
          :status="step.status === 'finish' ? 'finish' : step.status === 'error' ? 'error' : i === currentStep ? 'process' : 'wait'"
        />
      </a-steps>
    </SectionCard>

    <div class="workbench">
      <div class="workbench__main">
        <DocViewer :doc="docContent" card-title="文档预览">
          <template #extra>
            <a-button type="link" size="small">
              <UploadOutlined /> 重新上传
            </a-button>
          </template>
        </DocViewer>
      </div>
      <div class="workbench__side">
        <BidReviewPanel
          :risks="risks"
          :risk-summary="riskSummary"
          :chat-messages="chatMessages"
          @chat-send="handleChatSend"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { UploadOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import DocViewer from '@shared/web/components/DocViewer.vue'
import BidReviewPanel from './components/BidReviewPanel.vue'
import { getBidSteps, getBidRisks, getBidSessions, getBidDocument } from '@/api/modules/bid'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'
import type { ChatMessage } from '@shared/core/types/chat'

const steps = ref<BidReviewStep[]>([])
const risks = ref<RiskItem[]>([])
const sessions = ref<BidReviewSession[]>([])
const document = ref('')
const activeSessionId = ref('')
const chatMessages = ref<ChatMessage[]>([])

const currentStep = computed(() => Math.max(steps.value.findIndex((s) => s.status === 'process'), 0))
const currentPercent = computed(() => steps.value[currentStep.value]?.progress ?? 0)

const currentSession = computed(() => sessions.value.find((s) => s.id === activeSessionId.value))

const docContent = computed(() => document.value ? { title: currentSession.value?.document || '标书文档', content: document.value } : null)

const riskSummary = computed(() => [
  { key: 'high', label: '高风险', count: risks.value.filter((r) => r.level === '高风险').length },
  { key: 'mid', label: '中风险', count: risks.value.filter((r) => r.level === '中风险').length },
  { key: 'low', label: '低风险', count: risks.value.filter((r) => r.level === '低风险').length },
])

function handleChatSend(text: string): void {
  chatMessages.value.push({ role: 'user', content: text })
  setTimeout(() => {
    chatMessages.value.push({ role: 'assistant', content: '已收到您的追问，正在分析标书内容...' })
  }, 600)
}

onMounted(async () => {
  const [s, r, sess, doc] = await Promise.all([
    getBidSteps(),
    getBidRisks(),
    getBidSessions(),
    getBidDocument(),
  ])
  steps.value = s
  risks.value = r
  sessions.value = sess
  document.value = doc
  if (sess.length > 0) {
    activeSessionId.value = sess[0].id
    if (sess[0].snippets) {
      chatMessages.value = sess[0].snippets.map((s) => ({
        role: s.role as 'user' | 'assistant',
        content: s.content,
      }))
    }
  }
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.bid-read {
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  height: 100%;
}

.steps-card {
  flex-shrink: 0;
  :deep(.ant-steps-item-description) { font-size: @font-size-xs; color: @text-tertiary; }
}
</style>
