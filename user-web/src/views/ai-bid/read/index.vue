<template>
  <div class="bid-read">
    <a-row :gutter="[16, 16]">
      <a-col :xs="24" :md="6">
        <SectionCard title="审标步骤" class="bid-read__card mb-16">
          <a-steps :current="currentStep" direction="vertical" size="small">
            <a-step
              v-for="(step, i) in steps"
              :key="i"
              :title="step.title"
              :description="step.description"
              :status="step.status"
            />
          </a-steps>
        </SectionCard>
      </a-col>

      <a-col :xs="24" :md="12">
        <SectionCard title="文档预览" class="bid-read__card mb-16">
          <template #extra>
            <a-button type="link" size="small">
              <upload-outlined /> 重新上传
            </a-button>
          </template>
          <div class="doc-viewer">
            <pre class="doc-content">{{ document }}</pre>
          </div>
        </SectionCard>

        <SectionCard title="追问与对话" nopad class="bid-read__card">
          <div class="chat-area">
            <div class="chat-messages" ref="chatRef">
              <div
                v-for="(msg, i) in currentSession?.snippets || []"
                :key="i"
                class="chat-msg"
                :class="`chat-msg--${msg.role}`"
                :style="{ animationDelay: `${i * 0.05}s` }"
              >
                <div class="chat-avatar">{{ msg.role === 'user' ? '我' : 'AI' }}</div>
                <div class="chat-bubble">{{ msg.content }}</div>
              </div>
              <div v-if="!currentSession?.snippets?.length" class="chat-empty">
                <bulb-outlined />
                <span>暂无对话，上传文档后向 AI 追问标书细节</span>
              </div>
            </div>
            <div class="chat-input">
              <a-input-search
                v-model:value="questionInput"
                placeholder="输入追问内容..."
                enter-button="发送"
                @search="handleSendQuestion"
              />
            </div>
          </div>
        </SectionCard>
      </a-col>

      <a-col :xs="24" :md="6">
        <SectionCard title="风险清单" class="bid-read__card mb-16">
          <template #extra>
            <a-button type="link" size="small">
              <download-outlined /> 导出报告
            </a-button>
          </template>
          <div class="risk-summary">
            <div class="risk-stat" v-for="level in riskSummary" :key="level.label">
              <div class="risk-stat-num" :class="`risk-stat-num--${level.key}`">{{ level.count }}</div>
              <div class="risk-stat-label">{{ level.label }}</div>
            </div>
          </div>
        </SectionCard>

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
              <bulb-outlined />
              <span>{{ risk.suggestion }}</span>
            </div>
          </div>
        </transition-group>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { UploadOutlined, DownloadOutlined, BulbOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import { getBidSteps, getBidRisks, getBidSessions, getBidDocument } from '@/api/modules/bid'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

const steps = ref<BidReviewStep[]>([])
const risks = ref<RiskItem[]>([])
const sessions = ref<BidReviewSession[]>([])
const document = ref('')
const activeSessionId = ref('')
const questionInput = ref('')
const chatRef = ref<HTMLElement>()

const currentStep = computed(() => steps.value.findIndex((s) => s.status === 'process'))

const currentSession = computed(() => sessions.value.find((s) => s.id === activeSessionId.value))

const riskSummary = computed(() => [
  { key: 'high', label: '高风险', count: risks.value.filter((r) => r.level === '高风险').length },
  { key: 'mid', label: '中风险', count: risks.value.filter((r) => r.level === '中风险').length },
  { key: 'low', label: '低风险', count: risks.value.filter((r) => r.level === '低风险').length },
])

function handleSendQuestion(): void {
  if (!questionInput.value.trim()) return
  const snippet = currentSession.value?.snippets
  if (snippet) {
    snippet.push({ role: 'user', content: questionInput.value })
    snippet.push({ role: 'assistant', content: '已收到您的追问，正在分析标书内容...' })
  }
  questionInput.value = ''
  setTimeout(() => {
    chatRef.value?.scrollTo({ top: chatRef.value.scrollHeight, behavior: 'smooth' })
  }, 50)
}

onMounted(async () => {
  const [s, r, sess, doc] = await Promise.all([
    getBidSteps(), getBidRisks(), getBidSessions(), getBidDocument(),
  ])
  steps.value = s
  risks.value = r
  sessions.value = sess
  document.value = doc
  if (sess.length > 0) activeSessionId.value = sess[0].id
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.bid-read {
  &__card {
    box-shadow: @shadow-sm;
    transition: box-shadow @transition-base;
    &:hover { box-shadow: @shadow-md; }
  }
}

.mb-16 { margin-bottom: @spacing-lg; }

/* —— 文档预览 —— */
.doc-viewer {
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-lg;
  max-height: 400px;
  overflow-y: auto;
}
.doc-content {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: @font-size-sm;
  line-height: 1.8;
  color: @text-primary;
  white-space: pre-wrap;
}

/* —— 聊天 —— */
.chat-area { display: flex; flex-direction: column; height: 340px; }
.chat-messages {
  flex: 1; overflow-y: auto; padding: @spacing-lg;
  scroll-behavior: smooth;
}
.chat-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 100%; gap: @spacing-sm;
  color: @text-tertiary; font-size: @font-size-sm;
}
.chat-msg {
  display: flex; gap: @spacing-sm; margin-bottom: @spacing-md;
  animation: msg-in 0.3s ease both;
  &--user { flex-direction: row-reverse; }
}
@keyframes msg-in {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}
.chat-avatar {
  width: 30px; height: 30px; border-radius: 50%;
  background: @brand-gradient; color: white;
  display: flex; align-items: center; justify-content: center;
  font-size: 11px; font-weight: @font-weight-semibold;
  flex-shrink: 0;
}
.chat-bubble {
  background: @content-bg;
  padding: 8px 14px;
  border-radius: 14px 14px 14px 4px;
  font-size: @font-size-sm;
  max-width: 70%;
  line-height: 1.5;
  .chat-msg--user & {
    background: @brand-primary; color: white;
    border-radius: 14px 14px 4px 14px;
  }
}
.chat-input {
  padding: @spacing-md @spacing-lg;
  border-top: 1px solid @divider-color;
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

/* —— risk stagger animation —— */
.risk-stagger-enter-active {
  transition: all 0.3s ease;
}
.risk-stagger-leave-active {
  transition: all 0.2s ease;
}
.risk-stagger-enter-from {
  opacity: 0;
  transform: translateX(-12px);
}
.risk-stagger-leave-to {
  opacity: 0;
  transform: translateX(12px);
}
</style>

