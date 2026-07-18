<template>
  <div class="page-container bid-review">
    <a-row :gutter="[16, 16]">
      <a-col :span="6">
        <SectionCard title="审标步骤" class="mb-16">
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

        <SectionCard title="历史会话" nopad>
          <div class="session-list">
            <div
              v-for="session in sessions"
              :key="session.id"
              class="session-item"
              :class="{ active: session.id === activeSessionId }"
              @click="activeSessionId = session.id"
            >
              <div class="session-name">{{ session.document }}</div>
              <div class="session-meta">
                <span>{{ session.date }}</span>
                <a-badge :count="session.riskCount" :number-style="{ backgroundColor: session.riskCount > 0 ? badgeColors.danger : badgeColors.success }" />
              </div>
            </div>
          </div>
        </SectionCard>
      </a-col>

      <a-col :span="12">
        <SectionCard title="文档预览" class="mb-16">
          <template #extra>
            <a-button type="link" size="small">
              <upload-outlined />
              重新上传
            </a-button>
          </template>
          <div class="doc-viewer">
            <pre class="doc-content">{{ document }}</pre>
          </div>
        </SectionCard>

        <SectionCard title="追问与对话" nopad>
          <div class="chat-area">
            <div class="chat-messages">
              <div
                v-for="(msg, i) in currentSession?.snippets || []"
                :key="i"
                class="chat-msg"
                :class="`chat-msg--${msg.role}`"
              >
                <div class="chat-avatar">{{ msg.role === 'user' ? '我' : 'AI' }}</div>
                <div class="chat-bubble">{{ msg.content }}</div>
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

      <a-col :span="6">
        <SectionCard title="风险清单" class="mb-16">
          <template #extra>
            <a-button type="link" size="small">
              <download-outlined />
              导出报告
            </a-button>
          </template>
          <div class="risk-summary">
            <div class="risk-stat" v-for="level in riskSummary" :key="level.label">
              <div class="risk-stat-num" :class="`risk-stat-num--${level.key}`">{{ level.count }}</div>
              <div class="risk-stat-label">{{ level.label }}</div>
            </div>
          </div>
        </SectionCard>

        <div class="risk-list">
          <div
            v-for="risk in risks"
            :key="risk.id"
            class="risk-card"
            :class="`risk-card--${risk.level}`"
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
        </div>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watchEffect, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { UploadOutlined, DownloadOutlined, BulbOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import { getBidSteps, getBidRisks, getBidSessions, getBidDocument } from '@/api/modules/bid'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'
import { useTheme } from '@shared/composables/useTheme'
import { cssVarValue } from '@shared/composables/useCssVar'

const { currentTheme } = useTheme()
const badgeColors = reactive({ danger: '#EF4444', success: '#10B981' })

watchEffect(() => {
  currentTheme.value
  badgeColors.danger = cssVarValue('--color-danger')
  badgeColors.success = cssVarValue('--color-success')
})

const steps = ref<BidReviewStep[]>([])
const risks = ref<RiskItem[]>([])
const sessions = ref<BidReviewSession[]>([])
const document = ref('')
const activeSessionId = ref('')
const questionInput = ref('')

const currentStep = computed(() => steps.value.findIndex((s) => s.status === 'process'))

const currentSession = computed(() => sessions.value.find((s) => s.id === activeSessionId.value))

const riskSummary = computed(() => [
  { key: 'high', label: '高风险', count: risks.value.filter((r) => r.level === '高风险').length },
  { key: 'mid', label: '中风险', count: risks.value.filter((r) => r.level === '中风险').length },
  { key: 'low', label: '低风险', count: risks.value.filter((r) => r.level === '低风险').length },
])

function handleSendQuestion(): void {
  if (!questionInput.value.trim()) return
  message.success('已发送追问，AI 正在分析...')
  questionInput.value = ''
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
@import '@shared/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.session-list { max-height: 400px; overflow-y: auto; }
.session-item {
  padding: @spacing-md @spacing-xl;
  border-bottom: 1px solid @divider-color;
  cursor: pointer;
  transition: background @transition-base;
  &:hover { background: @content-bg; }
  &.active { background: color-mix(in srgb, @brand-primary 6%, transparent); border-left: 3px solid @brand-primary; }
}
.session-name { font-size: @font-size-sm; font-weight: @font-weight-medium; color: @text-primary; margin-bottom: 4px; }
.session-meta {
  display: flex; align-items: center; justify-content: space-between;
  font-size: @font-size-xs; color: @text-tertiary;
}

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

.chat-area { display: flex; flex-direction: column; height: 320px; }
.chat-messages { flex: 1; overflow-y: auto; padding: @spacing-lg; }
.chat-msg {
  display: flex; gap: @spacing-sm; margin-bottom: @spacing-md;
  &--user { flex-direction: row-reverse; }
}
.chat-avatar {
  width: 32px; height: 32px; border-radius: 50%;
  background: @brand-gradient; color: white;
  display: flex; align-items: center; justify-content: center;
  font-size: @font-size-xs; font-weight: @font-weight-semibold;
  flex-shrink: 0;
}
.chat-bubble {
  background: @content-bg;
  padding: @spacing-sm @spacing-md;
  border-radius: @radius-base;
  font-size: @font-size-sm;
  max-width: 70%;
  .chat-msg--user & { background: @brand-primary; color: white; }
}
.chat-input { padding: @spacing-md @spacing-lg; border-top: 1px solid @divider-color; }

.risk-summary {
  display: flex; justify-content: space-around;
  padding: @spacing-md 0;
}
.risk-stat { text-align: center; }
.risk-stat-num {
  font-size: @font-size-2xl; font-weight: @font-weight-bold;
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
</style>
