<template>
  <div class="analysis-view">
    <SectionCard :title="task.name" class="analysis-main">
      <div class="analysis-main__grid">
        <div class="overall">
          <a-progress
            type="circle"
            :percent="overallPct"
            :size="110"
            :status="task.status === 'failed' ? 'exception' : undefined"
          />
          <div class="overall__label">{{ isDone ? '分析完成' : '系统正在逐步分析' }}</div>
          <div class="overall__desc">{{ overallDesc }}</div>
        </div>

        <div class="stage-list">
          <div v-for="s in stages" :key="s.key" class="stage" :class="`stage--${s.status}`">
            <span class="stage__icon">
              <CheckCircleFilled v-if="s.status === 'finish'" />
              <LoadingOutlined v-else-if="s.status === 'process'" />
              <CloseCircleFilled v-else-if="s.status === 'error'" />
              <span v-else class="stage__num">{{ s.num }}</span>
            </span>
            <div class="stage__body">
              <div class="stage__title">
                {{ s.title }}
                <span v-if="s.status === 'process'" class="stage__pct">{{ s.pct }}%</span>
              </div>
              <div class="stage__desc">{{ s.desc }}</div>
              <a-progress
                v-if="s.status === 'process'"
                :percent="s.pct"
                size="small"
                :show-info="false"
                class="stage__bar"
              />
              <div v-if="s.live" class="stage__live" :class="{ 'stage__live--active': s.status === 'process' }">
                <span class="stage__live-dot" />
                {{ s.live }}
              </div>
              <div v-for="(b, bi) in s.bullets" :key="bi" class="stage__bullet">{{ b }}</div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="isDone" class="analysis-done">
        <CheckCircleFilled class="analysis-done__icon" />
        <div class="analysis-done__body">
          <div class="analysis-done__title">比对分析已完成</div>
          <div class="analysis-done__desc">{{ doneSummary }}</div>
        </div>
        <a-button type="primary" @click="emit('completed')">查看比标结果</a-button>
      </div>
      <div v-else-if="task.status === 'failed'" class="analysis-done analysis-done--failed">
        <CloseCircleFilled class="analysis-done__icon" />
        <div class="analysis-done__body">
          <div class="analysis-done__title">分析失败</div>
          <div class="analysis-done__desc">{{ failText }}</div>
        </div>
      </div>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { CheckCircleFilled, CloseCircleFilled, LoadingOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { CompareTask, EvidenceItem, RiskLevel } from '@/types'

const props = defineProps<{
  task: CompareTask
  evidence: EvidenceItem[]
}>()

const emit = defineEmits<{ completed: [] }>()

const isDone = computed(() => props.task.status === 'completed' || props.task.status === 'partial')

const totalPages = computed(() => props.task.documents.reduce((sum, d) => sum + (d.pages || 0), 0))
const bidCount = computed(() => props.task.documents.length)

const riskCounts = computed(() => {
  const s = props.task.riskSummary
  return {
    high: s?.high ?? props.evidence.filter((e) => e.severity === 'high').length,
    mid: s?.mid ?? props.evidence.filter((e) => e.severity === 'mid').length,
    low: s?.low ?? props.evidence.filter((e) => e.severity === 'low').length,
  }
})

interface Stage {
  key: string
  num: number
  title: string
  desc: string
  status: 'wait' | 'process' | 'finish' | 'error'
  pct: number
  /** 阶段实时行（该阶段的动态子状态） */
  live?: string
  /** 阶段下挂的证据/明细条目 */
  bullets?: string[]
}

const stages = computed<Stage[]>(() => {
  const t = props.task
  const p = t.progress
  const failed = t.status === 'failed'
  const n = bidCount.value
  return [
    {
      key: 'parse',
      num: 1,
      title: '文档解析',
      desc: '逐页解析 PDF，提取文本、版面与元数据',
      status: failed && p.parse < 100 ? 'error' : t.status === 'parsing' ? 'process' : p.parse >= 100 ? 'finish' : 'wait',
      pct: p.parse,
      live: p.parse >= 100
        ? `已解析 ${n} 份文件，共 ${totalPages.value} 页`
        : p.parse > 0
          ? `正在解析第 ${Math.min(n, Math.max(1, Math.ceil(p.parse / 100 * n)))} / ${n} 份…`
          : undefined,
    },
    {
      key: 'compare',
      num: 2,
      title: '交叉比对',
      desc: '两两比对文本、结构与报价相似度，识别雷同',
      status: failed && p.parse >= 100 && p.compare < 100 ? 'error' : t.status === 'comparing' ? 'process' : p.compare >= 100 ? 'finish' : 'wait',
      pct: p.compare,
      live: p.compare >= 100
        ? `比对完成，发现证据 ${props.evidence.length} 条`
        : p.compare > 0
          ? `正在两两比对… ${p.compare}%`
          : undefined,
      bullets: props.evidence.slice(0, 3).map((e) => `${severityText(e.severity)} · ${e.title}`),
    },
    {
      key: 'ai',
      num: 3,
      title: 'AI 综合研判',
      desc: '结合元数据、报价规律与条款响应判定围标风险',
      status: failed && p.compare >= 100 ? 'error' : t.status === 'ai_analyzing' ? 'process' : p.ai >= 100 ? 'finish' : 'wait',
      pct: p.ai,
      live: p.ai >= 100
        ? `研判完成：高 ${riskCounts.value.high} / 中 ${riskCounts.value.mid} / 低 ${riskCounts.value.low}`
        : p.ai > 0
          ? `正在综合研判… ${p.ai}%`
          : undefined,
    },
  ]
})

const overallPct = computed(() => {
  const p = props.task.progress
  if (isDone.value) return 100
  return Math.round(p.parse * 0.4 + p.compare * 0.35 + p.ai * 0.25)
})

const overallDesc = computed(() => {
  const cur = stages.value.find((s) => s.status === 'process')
  if (cur) return `当前阶段：${cur.title}`
  if (isDone.value) return `共 ${bidCount.value} 份文件 · ${totalPages.value} 页`
  return '排队等待分析…'
})

const doneSummary = computed(() =>
  `高风险 ${riskCounts.value.high} 条 / 中风险 ${riskCounts.value.mid} 条 / 低风险 ${riskCounts.value.low} 条，进入结果页查看明细与原文溯源`,
)

const failText = computed(() => {
  const failed = props.task.documents.find((d) => d.parseStatus === 'failed')
  return failed?.failReason || '部分文档解析失败，可从历史记录重新打开重试'
})

function severityText(s: RiskLevel): string {
  return s === 'high' ? '高风险' : s === 'mid' ? '中风险' : '低风险'
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.analysis-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.analysis-main {
  flex: 1;
  min-height: 0;
  overflow: auto;

  &__grid {
    display: grid;
    grid-template-columns: 240px minmax(0, 1fr);
    gap: @spacing-xl;
    align-items: start;
  }
}

.overall {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: @spacing-sm;
  padding: @spacing-md 0;

  &__label {
    font-size: @font-size-lg;
    font-weight: @font-weight-semibold;
    color: @text-primary;
  }

  &__desc {
    font-size: @font-size-sm;
    color: @text-secondary;
  }
}

.stage-list {
  display: flex;
  flex-direction: column;
}

.stage {
  display: flex;
  gap: @spacing-md;
  padding: @spacing-md 0;
  border-bottom: 1px solid @divider-color;

  &:last-child {
    border-bottom: none;
  }

  &__icon {
    width: 28px;
    height: 28px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    font-size: 16px;
  }

  &__num {
    width: 28px;
    height: 28px;
    border-radius: 50%;
    border: 1px solid @border-color;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__body {
    flex: 1;
    min-width: 0;
  }

  &__title {
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
  }

  &__pct {
    margin-left: @spacing-sm;
    font-size: @font-size-xs;
    color: @brand-primary;
    font-variant-numeric: tabular-nums;
  }

  &__desc {
    font-size: @font-size-xs;
    color: @text-tertiary;
    margin-top: 2px;
  }

  &__bar {
    margin-top: @spacing-sm;
    max-width: 560px;
  }

  &--finish &__icon { color: @success; }
  &--process &__icon { color: @brand-primary; }
  &--process &__title { color: @brand-primary; }
  &--error &__icon { color: @danger; }

  &__live {
    margin-top: @spacing-sm;
    display: flex;
    align-items: center;
    gap: @spacing-xs;
    font-size: @font-size-xs;
    color: @text-secondary;
  }

  &__live-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: @success;
    flex-shrink: 0;
  }

  &__live--active &__live-dot {
    background: @brand-primary;
    animation: live-pulse 1.2s ease-in-out infinite;
  }

  &__bullet {
    margin-top: @spacing-xs;
    padding-left: @spacing-md;
    font-size: @font-size-xs;
    color: @text-tertiary;
    line-height: 1.6;
  }
}

.analysis-done {
  margin-top: @spacing-xl;
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: @spacing-md @spacing-lg;
  border: 1px solid color-mix(in srgb, @success 30%, transparent);
  border-radius: @radius-base;
  background: color-mix(in srgb, @success 6%, transparent);

  &__icon {
    font-size: 24px;
    color: @success;
    flex-shrink: 0;
  }

  &__body {
    flex: 1;
    min-width: 0;
  }

  &__title {
    font-size: @font-size-sm;
    font-weight: @font-weight-semibold;
    color: @text-primary;
  }

  &__desc {
    font-size: @font-size-xs;
    color: @text-secondary;
    margin-top: 2px;
  }

  &--failed {
    border-color: color-mix(in srgb, @danger 30%, transparent);
    background: color-mix(in srgb, @danger 6%, transparent);

    .analysis-done__icon { color: @danger; }
  }
}

@media (max-width: 991px) {
  .analysis-main__grid {
    grid-template-columns: minmax(0, 1fr);
  }
}

@keyframes live-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}

@media (prefers-reduced-motion: reduce) {
  .stage__live--active .stage__live-dot { animation: none; }
}
</style>
