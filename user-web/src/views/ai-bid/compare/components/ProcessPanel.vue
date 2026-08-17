<template>
  <div class="process-panel">
    <div class="process-panel__scroll">
      <section
        v-for="(stage, index) in visibleStages"
        :key="stage.key"
        class="trace-stage"
        :class="{ 'trace-stage--collapsed': isCollapsed(stage.key) }"
      >
        <div class="trace-stage__head-wrap">
          <div
            role="button"
            tabindex="0"
            class="trace-stage__head"
            :class="{ 'trace-stage__head--active': !isStageDone(stage.key) }"
            @click="toggleStage(stage.key)"
            @keydown.enter="toggleStage(stage.key)"
            @keydown.space.prevent="toggleStage(stage.key)"
          >
            <span class="trace-stage__index">{{ index + 1 }}</span>
            <span class="trace-stage__title">{{ stage.title }}</span>
            <span class="trace-stage__summary">{{ summaryOf(stage.key) }}</span>
            <span class="trace-stage__spacer" />
            <AppButton
              v-if="stage.key === 'compare' && canRetryCompare"
              size="sm"
              variant="secondary"
              class="trace-stage__retry"
              :loading="retryingCompare"
              @click.stop="emit('retryCompare')"
            >
              重新对比
            </AppButton>
            <AppButton
              v-if="(stage.key === 'ai-clause' || stage.key === 'ai-indicator') && canRetryAi"
              size="sm"
              variant="secondary"
              class="trace-stage__retry"
              :loading="retryingAi"
              @click.stop="emit('retryAi')"
            >
              重新抽取
            </AppButton>
            <a-tag :color="metaOf(stage.key).color" class="trace-stage__tag">{{ metaOf(stage.key).text }}</a-tag>
            <DownOutlined
              v-if="isStageDone(stage.key)"
              class="trace-stage__chevron"
              :class="{ 'trace-stage__chevron--collapsed': isCollapsed(stage.key) }"
            />
          </div>
        </div>

        <div v-show="!isCollapsed(stage.key)" class="trace-stage__body">
          <template v-if="stage.key === 'parse'">
            <div class="process-list">
              <div
                v-for="d in parseDocuments"
                :key="d.id"
                class="process-row"
              >
                <DocBadge
                  v-if="d.role !== 'tender'"
                  :label="docLabel(task.documents, d.id)"
                />
                <a-tag v-if="d.role === 'tender'" class="process-row__tender">招标</a-tag>
                <template v-if="d.parseStatus === 'parsing'">
                  <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
                  <a-progress
                    class="process-row__parse-bar"
                    :percent="docParsePercent(d)"
                    :show-info="false"
                    size="small"
                  />
                  <span class="process-row__step" :title="stepText(d)">{{ stepText(d) }}</span>
                  <span class="process-row__percent">{{ docParsePercent(d) }}%</span>
                  <span class="process-row__elapsed">已用 {{ elapsedText(d) }}</span>
                  <a-spin size="small" />
                </template>
                <template v-else>
                  <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
                  <span class="process-row__spacer" />
                  <span v-if="d.pages" class="process-row__pages">{{ d.pages }} 页</span>
                  <span v-if="parseDurationText(d)" class="process-row__done-time">
                    解析耗时 {{ parseDurationText(d) }}
                  </span>
                  <span v-if="d.failReason" class="process-row__error" :title="d.failReason">{{ d.failReason }}</span>
                  <AppButton
                    v-if="d.parseStatus === 'failed'"
                    variant="link"
                    size="sm"
                    :loading="reparseDocIds.includes(d.id)"
                    @click="emit('reparseDoc', d.id)"
                  >
                    重新解析
                  </AppButton>
                  <CheckCircleFilled v-if="d.parseStatus === 'done'" class="process-row__ok" />
                  <CloseCircleFilled v-else-if="d.parseStatus === 'failed'" class="process-row__bad" />
                  <span v-else class="process-row__wait">等待</span>
                </template>
              </div>
              <a-empty v-if="!task.documents.length" description="暂无文档" />
            </div>
            <div v-if="failedDocs.length" class="process-panel__parse-conclusion">
              <ExclamationCircleOutlined class="process-panel__parse-conclusion-icon" />
              <span class="process-panel__parse-conclusion-text">{{ parseConclusionText }}</span>
              <AppButton
                size="sm"
                :loading="reparseAllLoading"
                @click="emit('reparseAll')"
              >
                重新解析失败文档
              </AppButton>
            </div>
          </template>

          <template v-else-if="stage.key === 'clause'">
            <div v-if="extracting" class="process-panel__skeleton">
              <a-skeleton active :paragraph="{ rows: 3 }" />
            </div>
            <a-empty v-else-if="!editableDrafts.length" description="尚未提取条款">
              <AppButton variant="primary" size="sm" @click="emit('extractClauses')">提取条款</AppButton>
            </a-empty>
            <div v-else class="clause-edit">
              <div v-for="(c, i) in editableDrafts" :key="c.id" class="clause-edit__row">
                <a-tag :color="c.mandatory ? 'red' : 'default'" class="clause-edit__tag">
                  {{ c.mandatory ? '强制' : '建议' }}
                </a-tag>
                <a-tag class="clause-edit__source">{{ sourceText(c.source) }}</a-tag>
                <a-input
                  v-model:value="editableDrafts[i].content"
                  size="small"
                  :placeholder="c.title"
                  class="clause-edit__input"
                />
                <AppButton variant="text" size="sm" @click="removeClause(i)">
                  <DeleteOutlined />
                </AppButton>
              </div>
              <AppButton size="sm" variant="dashed" block @click="addClause">
                <PlusOutlined />添加条款
              </AppButton>
              <div class="clause-edit__footer">
                <span class="clause-edit__hint">确认后锁定任务快照，进入两两对比</span>
                <AppButton
                  variant="primary"
                  :loading="confirmingClauses"
                  :disabled="!editableDrafts.length"
                  @click="emit('confirmClauses', editableDrafts.map(toPayload))"
                >
                  确认并继续
                </AppButton>
              </div>
            </div>
          </template>

          <template v-else-if="stage.key === 'compare'">
            <template v-if="comparing">
              <div class="process-list">
                <div v-for="p in pairs" :key="p.pairId" class="process-row">
                  <span class="process-row__label process-row__label--pair">
                    <DocBadge :label="docLabel(task.documents, p.docAId)" />
                    <span class="process-row__arrow">→</span>
                    <DocBadge :label="docLabel(task.documents, p.docBId)" />
                  </span>
                  <template v-if="p.status === 'done' || p.status === 'failed'">
                    <a-tag :color="PAIR_META[p.status].color" class="process-row__status">{{ PAIR_META[p.status].text }}</a-tag>
                    <span v-if="p.similarity != null" class="process-row__sim">
                      相似度 {{ Math.round(p.similarity * 100) }}%
                    </span>
                    <span v-if="p.failReason" class="process-row__error" :title="p.failReason">{{ p.failReason }}</span>
                    <AppButton
                      v-if="p.status === 'failed'"
                      variant="link"
                      size="sm"
                      :loading="retryingPairIds.includes(p.pairId)"
                      @click="emit('retryPair', p.pairId)"
                    >
                      重试该对
                    </AppButton>
                  </template>
                  <template v-else>
                    <a-spin size="small" />
                    <span class="process-row__step">比对中…</span>
                    <span class="process-row__elapsed">已用 {{ pairElapsedText(p) }}</span>
                  </template>
                </div>
                <a-empty v-if="!pairs.length" description="比对对将在解析完成后生成" />
              </div>
            </template>
            <template v-else>
              <SimilarityHeatmap
                v-if="overview && overview.docLabels.length"
                :labels="overview.docLabels"
                :matrix="overview.simMatrix"
                @cell-click="onHeatmapCell"
              />

              <UnifyCompareTable
                v-if="passageEvidences.length || compareEvidence.length"
                :passage-evidence="passageEvidences"
                :compare-evidence="compareEvidence"
                :documents="task.documents"
                :task-id="task.id"
                @locate="(e) => emit('locate', e)"
                @locate-refs="(refs) => emit('locateRefs', refs)"
              />

              <div v-if="failedPairs.length || !pairs.length" class="process-list">
                <div v-for="p in failedPairs" :key="p.pairId" class="process-row">
                  <span class="process-row__label process-row__label--pair">
                    <DocBadge :label="docLabel(task.documents, p.docAId)" />
                    <span class="process-row__arrow">↔</span>
                    <DocBadge :label="docLabel(task.documents, p.docBId)" />
                  </span>
                  <a-tag :color="PAIR_META[p.status].color" class="process-row__status">{{ PAIR_META[p.status].text }}</a-tag>
                  <span v-if="p.similarity != null" class="process-row__sim">
                    相似度 {{ Math.round(p.similarity * 100) }}%
                  </span>
                  <span v-if="p.failReason" class="process-row__error" :title="p.failReason">{{ p.failReason }}</span>
                  <AppButton
                    v-if="p.status === 'failed'"
                    variant="link"
                    size="sm"
                    :loading="retryingPairIds.includes(p.pairId)"
                    @click="emit('retryPair', p.pairId)"
                  >
                    重试该对
                  </AppButton>
                </div>
                <a-empty v-if="!pairs.length" description="比对对将在解析完成后生成" />
              </div>
            </template>
          </template>

          <template v-else-if="stage.key === 'ai-clause'">
            <div v-if="aiUnavailable" class="process-panel__ai-alert">
              <a-alert
                type="warning"
                show-icon
                message="AI 分析暂不可用"
                description="算法证据不受影响，可稍后重试"
              />
            </div>
            <div v-else-if="!aiDone" class="process-list">
              <div v-for="(phase, i) in aiPhases" :key="phase.key" class="process-row">
                <CheckCircleFilled v-if="aiActivePhase !== -1 && i < aiActivePhase" class="process-row__ok" />
                <a-spin v-else-if="aiActivePhase === -1 || i === aiActivePhase" size="small" />
                <span
                  class="process-row__name"
                  :class="{ 'process-row__name--pending': aiActivePhase !== -1 && i > aiActivePhase }"
                >{{ phase.label }}</span>
              </div>
            </div>
            <template v-if="aiDone">
              <ResponseMatrix
                :documents="task.documents"
                :evidence="evidence"
                @trace="(e) => emit('locate', e)"
              />
              <template v-if="clauseEvidence.length">
                <div class="trace-stage__subtitle">条款未响应证据</div>
                <div class="process-feed">
                  <EvidenceCard
                    v-for="ev in clauseEvidence"
                    :key="ev.id"
                    :evidence="ev"
                    :documents="task.documents"
                    @trace="(e) => emit('locate', e)"
                    @trace-ref="(refs) => emit('locateRefs', refs)"
                  />
                </div>
              </template>
            </template>
          </template>

          <template v-else-if="stage.key === 'ai-indicator'">
            <div v-if="aiUnavailable" class="process-panel__ai-alert">
              <a-alert
                type="warning"
                show-icon
                message="AI 分析暂不可用"
                description="算法证据不受影响，可稍后重试"
              />
            </div>
            <div v-else-if="!aiDone" class="process-list">
              <div v-for="(phase, i) in aiPhases" :key="phase.key" class="process-row">
                <CheckCircleFilled v-if="aiActivePhase !== -1 && i < aiActivePhase" class="process-row__ok" />
                <a-spin v-else-if="aiActivePhase === -1 || i === aiActivePhase" size="small" />
                <span
                  class="process-row__name"
                  :class="{ 'process-row__name--pending': aiActivePhase !== -1 && i > aiActivePhase }"
                >{{ phase.label }}</span>
              </div>
            </div>
            <template v-if="aiDone">
              <IndicatorTable
                :evidence="evidence"
                :documents="task.documents"
                @trace="(e) => emit('locate', e)"
              />
            </template>
          </template>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { AppButton } from '@shared/web'
import { message } from 'ant-design-vue'
import {
  CheckCircleFilled,
  CloseCircleFilled,
  DeleteOutlined,
  DownOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
} from '@ant-design/icons-vue'
import EvidenceCard from './EvidenceCard.vue'
import SimilarityHeatmap from './SimilarityHeatmap.vue'
import UnifyCompareTable from './UnifyCompareTable.vue'
import ResponseMatrix from './ResponseMatrix.vue'
import IndicatorTable from './IndicatorTable.vue'
import { anGineerStepInfo, docLabel } from '../constants'
import DocBadge from './DocBadge.vue'
import type { BlockRange, ClauseItem, CompareDocMeta, ComparePair, CompareTask, EvidenceItem, TaskOverview } from '@/types'

type StageKey = 'parse' | 'clause' | 'compare' | 'ai-clause' | 'ai-indicator'

const props = defineProps<{
  task: CompareTask
  overview: TaskOverview | null
  evidence: EvidenceItem[]
  clauseDrafts: ClauseItem[]
  extracting: boolean
  confirmingClauses: boolean
  reparseDocIds: string[]
  reparseAllLoading: boolean
  retryingPairIds: string[]
  retryingCompare: boolean
  retryingAi: boolean
}>()

const emit = defineEmits<{
  reparseDoc: [docId: string]
  reparseAll: []
  retryPair: [pairId: string]
  retryCompare: []
  retryAi: []
  extractClauses: []
  confirmClauses: [clauses: ClauseItem[]]
  locate: [item: EvidenceItem]
  locateRefs: [refs: BlockRange[]]
}>()

const nowTick = ref(Date.now())
let timerHandle: number | undefined

function stopParseTimer(): void {
  if (timerHandle !== undefined) {
    window.clearInterval(timerHandle)
    timerHandle = undefined
  }
}

function startParseTimer(): void {
  if (timerHandle !== undefined) return
  nowTick.value = Date.now()
  timerHandle = window.setInterval(() => {
    nowTick.value = Date.now()
  }, 1000)
}

watch(() => [props.task.documents, props.task.status] as const, ([docs, status]) => {
  const parsing = docs.some((d) => d.parseStatus === 'parsing')
  if (parsing || status === 'comparing') {
    startParseTimer()
  } else {
    stopParseTimer()
  }
}, { immediate: true })

onBeforeUnmount(stopParseTimer)

function formatSeconds(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds))
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return minutes > 0 ? `${minutes}分${rest}秒` : `${seconds}秒`
}

/** 后端解析时间按 UTC 存储，DB 回读后序列化可能不带 Z；无时区标记时按 UTC 解析，避免被当成本地时间多算 8 小时。 */
function parseServerTime(value: string | undefined): number | undefined {
  if (!value) return undefined
  const normalized = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`
  const time = Date.parse(normalized)
  return Number.isNaN(time) ? undefined : time
}

function elapsedText(d: CompareDocMeta): string {
  const start = parseServerTime(d.parseStartedAt)
  if (!start) return '0秒'
  const end = parseServerTime(d.parseFinishedAt) ?? nowTick.value
  return formatSeconds((end - start) / 1000)
}

function parseDurationText(d: CompareDocMeta): string {
  if (d.parseStatus !== 'done' && d.parseStatus !== 'failed') return ''
  if (!d.parseStartedAt || !d.parseFinishedAt) return ''
  const start = parseServerTime(d.parseStartedAt)
  const end = parseServerTime(d.parseFinishedAt)
  if (start == null || end == null) return ''
  return formatSeconds((end - start) / 1000)
}

function pairElapsedText(p: ComparePair): string {
  const start = parseServerTime(p.startedAt)
  if (!start) return '0 秒'
  return formatSeconds((nowTick.value - start) / 1000)
}

function stepText(d: CompareDocMeta): string {
  const info = anGineerStepInfo(d.parseStage)
  const step = info ? `步骤 ${info.step}/${info.total}` : ''
  const parts = [step, d.parseStage, d.parseStageMessage].filter((s): s is string => !!s)
  return parts.length ? parts.join(' · ') : '解析中'
}

/** 单文档解析进度：优先按阶段推导（AnGIneer progress 粒度粗），有真实进度时取较大值。 */
function docParsePercent(d: CompareDocMeta): number {
  const info = anGineerStepInfo(d.parseStage)
  return Math.max(info?.percent ?? 0, d.parseProgress ?? 0)
}

const PAIR_META: Record<ComparePair['status'], { color: string, text: string }> = {
  waiting: { color: 'default', text: '等待' },
  processing: { color: 'blue', text: '比对中' },
  done: { color: 'green', text: '完成' },
  failed: { color: 'red', text: '失败' },
}

/** 解析列表：已完成置顶，其余保持上传顺序（Array.sort 稳定）。 */
const parseDocuments = computed(() =>
  [...props.task.documents].sort(
    (a, b) => (a.parseStatus === 'done' ? 0 : 1) - (b.parseStatus === 'done' ? 0 : 1),
  ),
)
const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))
const allDocsFailed = computed(() =>
  props.task.documents.length > 0 && props.task.documents.every((d) => d.parseStatus === 'failed'),
)
const parsedBidCount = computed(() =>
  props.task.documents.filter((d) => d.role !== 'tender' && d.parseStatus === 'done').length,
)
const parseConclusionText = computed(() => {
  if (parsedBidCount.value < 2) {
    return '文档解析失败：可用标书不足 2 份，请先重新解析失败文档后再重新对比'
  }
  return `已跳过 ${failedDocs.value.length} 份失败文档，其余结果不受影响`
})
const pairs = computed(() => props.task.pairs ?? [])
const failedPairs = computed(() => pairs.value.filter((p) => p.status === 'failed'))
const comparing = computed(() => props.task.status === 'comparing')
const bidCount = computed(() => props.task.documents.filter((d) => d.role !== 'tender').length)
const compareEvidence = computed(() =>
  props.evidence.filter((e) =>
    (e.type === 'similarity' || e.type === 'price' || e.type === 'metadata')
    && e.metrics?.kind !== 'passage'))
const passageEvidences = computed(() =>
  props.evidence.filter((e) => e.type === 'similarity' && e.metrics?.kind === 'passage'))
const clauseEvidence = computed(() => props.evidence.filter((e) => e.type === 'clause'))
const indicatorEvidence = computed(() => props.evidence.filter((e) => e.type === 'indicator'))
const aiUnavailable = computed(() => (props.task.progress.message ?? '').includes('AI 分析暂不可用'))

/** AI 分析子步骤：后端按序上报进度消息，前端据此高亮当前步骤（无招标文件时跳过条款判定）。 */
interface AiPhase { key: string, label: string }

const aiPhases = computed<AiPhase[]>(() => {
  const list: AiPhase[] = [{ key: '关键指标抽取', label: '关键指标抽取' }]
  if (props.task.tenderDocId) {
    list.unshift({ key: '条款响应判定', label: `条款响应判定（${bidCount.value} 份标书）` })
  }
  return list
})
const aiActivePhase = computed(() => {
  const msg = props.task.progress.message ?? ''
  return aiPhases.value.findIndex((p) => msg.includes(p.key))
})

function isTerminalish(t: CompareTask): boolean {
  return t.status === 'completed' || t.status === 'failed' || t.status === 'partial'
}

const parseDone = computed(() =>
  props.task.documents.length > 0
  && props.task.documents.every((d) => d.parseStatus === 'done' || d.parseStatus === 'failed'),
)
const clauseVisible = computed(() =>
  !!props.task.tenderDocId && !props.task.clauseSnapshot && !isTerminalish(props.task),
)
const clauseDone = computed(() => !!props.task.clauseSnapshot)
const compareVisible = computed(() =>
  parseDone.value
  && (!clauseVisible.value || clauseDone.value)
  && props.task.status !== 'failed'
  && props.task.status !== 'uploading',
)
const compareDone = computed(() => {
  if (isTerminalish(props.task) || props.task.progress.stage === 'analyzing' || props.task.progress.stage === 'done') {
    return true
  }
  return pairs.value.length > 0
    && pairs.value.every((p) => p.status === 'done' || p.status === 'failed')
    && props.task.status !== 'comparing'
    && props.task.status !== 'parsing'
})
const aiVisible = computed(() =>
  compareDone.value
  && props.task.status !== 'failed'
  && (props.task.progress.stage === 'analyzing'
    || props.task.progress.stage === 'done'
    || isTerminalish(props.task)),
)
const aiDone = computed(() => isTerminalish(props.task) || props.task.progress.stage === 'done')
const canRetryAi = computed(() => aiVisible.value && (aiDone.value || aiUnavailable.value))

const visibleStages = computed<{ key: StageKey, title: string }[]>(() => {
  const list: { key: StageKey, title: string }[] = [{ key: 'parse', title: '文档解析' }]
  if (clauseVisible.value) list.push({ key: 'clause', title: '条款确认' })
  if (compareVisible.value) list.push({ key: 'compare', title: '两两对比' })
  if (aiVisible.value) {
    list.push({ key: 'ai-indicator', title: '关键指标抽取（AI）' })
    if (props.task.tenderDocId) {
      list.push({ key: 'ai-clause', title: '强制性条款响应矩阵（AI）' })
    }
  }
  return list
})

const stageDoneMap = computed<Record<StageKey, boolean>>(() => ({
  'parse': parseDone.value,
  'clause': clauseDone.value,
  'compare': compareDone.value,
  'ai-clause': aiDone.value,
  'ai-indicator': aiDone.value,
}))

function isStageDone(key: StageKey): boolean {
  return stageDoneMap.value[key]
}

const expandedStages = ref<Set<StageKey>>(new Set())

function isCollapsed(key: StageKey): boolean {
  if (!isStageDone(key)) return false
  if (key === 'parse' && allDocsFailed.value) return false
  return !expandedStages.value.has(key)
}

function toggleStage(key: StageKey): void {
  if (!isStageDone(key)) return
  const next = new Set(expandedStages.value)
  if (next.has(key)) {
    next.delete(key)
  } else {
    next.add(key)
  }
  expandedStages.value = next
}

const parseDoneCount = computed(() =>
  props.task.documents.filter((d) => d.parseStatus === 'done').length,
)
const parseTotalCount = computed(() => props.task.documents.length)
const parsePercent = computed(() =>
  parseTotalCount.value > 0
    ? Math.round((parseDoneCount.value / parseTotalCount.value) * 100)
    : 0,
)

const parseSummary = computed(() => {
  if (!parseDone.value) {
    const total = props.task.documents.length
    const done = parseDoneCount.value
    const parsing = props.task.documents.filter((d) => d.parseStatus === 'parsing').length
    // AnGIneer progress 粒度较粗，总体进度按"已完成文档数/总数"计算（如 1/8 · 13%）
    const base = total ? `已解析 ${done}/${total} · ${parsePercent.value}%` : '等待解析'
    return parsing ? `${base} · 解析中 ${parsing}` : base
  }
  if (allDocsFailed.value) {
    return `文档解析失败 · ${failedDocs.value.length} 份`
  }
  const pages = props.task.documents.reduce((acc, d) => acc + (d.pages || 0), 0)
  const failed = failedDocs.value.length
  return `文档解析完成 · ${props.task.documents.length} 份 · ${pages} 页${failed ? ` · ${failed} 份失败` : ''}`
})

const clauseCount = computed(() => props.task.clauseSnapshot?.length ?? 0)
const clauseSummary = computed(() =>
  clauseDone.value ? `条款确认完成 · ${clauseCount.value} 条` : '等待确认')

const compareSummary = computed(() => {
  if (comparing.value) {
    return `比对中 · ${pairs.value.length} 对`
  }
  if (!compareDone.value) {
    const processing = pairs.value.find((p) => p.status === 'processing')
    if (processing) {
      const fallbackIndex = pairs.value.findIndex((p) => p.pairId === processing.pairId) + 1
      const idx = props.task.progress.pairIndex ?? (fallbackIndex > 0 ? fallbackIndex : 1)
      return `第 ${idx}/${pairs.value.length || '?'} 对比对中`
    }
    const done = pairs.value.filter((p) => p.status === 'done' || p.status === 'failed').length
    return done ? `已完成 ${done}/${pairs.value.length} 对` : '等待比对'
  }
  if (!pairs.value.length) return '两两对比完成'
  const done = pairs.value.filter((p) => p.status === 'done').length
  const sims = pairs.value.filter((p) => p.similarity != null).map((p) => p.similarity!)
  const max = sims.length ? Math.round(Math.max(...sims) * 100) : 0
  const failed = failedPairs.value.length
  return `两两对比完成 · ${done}/${pairs.value.length} 对 · 最高相似度 ${max}%${failed ? ` · ${failed} 对失败` : ''}`
})

/** 解析已完成但尚未触发比对（重新解析后不自动重跑，v2 §5.3）：展示「重新对比」入口 */
const canRetryCompare = computed(() => {
  const s = props.task.status
  return parseDone.value
    && s !== 'comparing'
    && s !== 'ai_analyzing'
    && s !== 'failed'
    && s !== 'uploading'
})

const aiClauseSummary = computed(() => {
  if (aiUnavailable.value) return 'AI 暂不可用，可重试'
  return aiDone.value ? `条款响应判定完成 · ${clauseEvidence.value.length} 条未响应` : '条款响应判定中'
})

const aiIndicatorSummary = computed(() => {
  if (aiUnavailable.value) return 'AI 暂不可用，可重试'
  return aiDone.value ? `关键指标抽取完成 · ${indicatorEvidence.value.length} 条发现` : '关键指标抽取中'
})

function summaryOf(key: StageKey): string {
  switch (key) {
    case 'parse': return parseSummary.value
    case 'clause': return clauseSummary.value
    case 'compare': return compareSummary.value
    case 'ai-clause': return aiClauseSummary.value
    case 'ai-indicator': return aiIndicatorSummary.value
  }
}

interface StageMeta {
  color: string
  text: string
}

const aiStageMeta = computed<StageMeta>(() => {
  if (aiUnavailable.value) return { color: 'orange', text: '暂不可用' }
  return aiDone.value ? { color: 'green', text: '完成' } : { color: 'purple', text: '分析中' }
})

const stageMeta = computed<Record<StageKey, StageMeta>>(() => ({
  'parse': parseDone.value
    ? failedDocs.value.length
      ? (allDocsFailed.value
          ? { color: 'red', text: '失败' }
          : { color: 'orange', text: '部分完成' })
      : { color: 'green', text: '完成' }
    : {
        color: props.task.documents.some((d) => d.parseStatus === 'parsing') ? 'blue' : 'default',
        text: props.task.documents.some((d) => d.parseStatus === 'parsing') ? '解析中' : '等待',
      },
  'clause': clauseDone.value ? { color: 'green', text: '完成' } : { color: 'gold', text: '待确认' },
  'compare': compareDone.value
    ? { color: failedPairs.value.length ? 'orange' : 'green', text: failedPairs.value.length ? '部分完成' : '完成' }
    : {
        color: comparing.value || pairs.value.some((p) => p.status === 'processing') ? 'blue' : 'default',
        text: comparing.value || pairs.value.some((p) => p.status === 'processing') ? '比对中' : '等待',
      },
  'ai-clause': aiStageMeta.value,
  'ai-indicator': aiStageMeta.value,
}))

function metaOf(key: StageKey): StageMeta {
  return stageMeta.value[key]
}

const editableDrafts = ref<ClauseItem[]>([])
watch(() => props.clauseDrafts, (list) => {
  editableDrafts.value = list.map((c) => ({ ...c }))
}, { immediate: true, deep: true })

function sourceText(source: ClauseItem['source']): string {
  return {
    library: '模板库',
    ai_extracted: 'AI 提取',
    user_added: '手动添加',
  }[source] ?? source
}

function addClause(): void {
  editableDrafts.value.push({
    id: `draft-${Date.now()}`,
    title: '',
    content: '',
    category: '',
    mandatory: true,
    source: 'user_added',
  })
}

function removeClause(index: number): void {
  editableDrafts.value.splice(index, 1)
}

function toPayload(c: ClauseItem): ClauseItem {
  return {
    ...c,
    content: c.content || c.title,
    title: c.content || c.title,
  }
}

function onHeatmapCell(pair: { docA: string, docB: string }): void {
  const labels = props.overview?.docLabels ?? []
  const bids = props.task.documents.filter((d) => d.role !== 'tender')
  const docAId = bids[labels.indexOf(pair.docA)]?.id
  const docBId = bids[labels.indexOf(pair.docB)]?.id
  const ev = props.evidence.find((e) =>
    docAId && docBId && e.docIds.includes(docAId) && e.docIds.includes(docBId))
  if (ev) {
    emit('locate', ev)
  } else {
    message.info('该文档对暂无发现')
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.process-panel {
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

.process-panel__scroll {
  height: 100%;
  overflow: auto;
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  padding: @spacing-md @spacing-base @spacing-xl;
}

.process-panel__parse-conclusion {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin: 0 @spacing-xl @spacing-xl;
  padding: @spacing-sm @spacing-md;
  border: 1px solid @danger;
  border-radius: @radius-base;
  background: color-mix(in srgb, @danger 8%, @card-bg);

  &-icon { color: @danger; flex-shrink: 0; }
  &-text { flex: 1; min-width: 0; font-size: @font-size-xs; color: @text-secondary; }
}

.trace-stage {
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  background: @card-bg;
  overflow: hidden;
  flex-shrink: 0;

  &__head-wrap {
    display: flex;
    align-items: stretch;
  }

  &__head {
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    padding: @spacing-md @spacing-xl;
    background: @card-bg;
    border: none;
    cursor: pointer;
    font: inherit;
    text-align: left;

    &:hover:not(.trace-stage__head--active) {
      background: color-mix(in srgb, @brand-primary 4%, @card-bg);
    }

    &:focus-visible {
      outline: 2px solid @brand-primary;
      outline-offset: -2px;
    }
  }

  &__retry {
    flex-shrink: 0;
    align-self: center;
    margin: 0 @spacing-md;
    background: @theme-dot-dark;
    border-color: @theme-dot-dark;
    color: #fff;
    font-size: @font-size-xs;

    &:hover,
    &:focus {
      background: @text-secondary;
      border-color: @text-secondary;
      color: #fff;
    }
  }

  &__head--active {
    cursor: default;
  }

  &__index {
    flex-shrink: 0;
    width: 22px;
    height: 22px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    background: @brand-primary;
    color: #fff;
    font-size: @font-size-xs;
    font-weight: @font-weight-semibold;
  }

  &__title {
    flex-shrink: 0;
    font-size: @font-size-sm;
    font-weight: @font-weight-semibold;
    color: @text-primary;
  }

  &__summary {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__spacer { flex: 1; }
  &__tag { flex-shrink: 0; margin-inline-end: 0; }

  &__chevron {
    flex-shrink: 0;
    color: @text-tertiary;
    transition: transform @transition-fast;

    &--collapsed { transform: rotate(-90deg); }
  }

  &__body {
    border-top: 1px solid @divider-color;
  }

  &__subtitle {
    padding: 10px @spacing-xl 0;
    font-size: @font-size-base;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    line-height: 1.4;
  }
}

.process-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
  padding: @spacing-base @spacing-xl @spacing-xl;
}

.process-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: 5px 0;
  font-size: @font-size-sm;

  &__label {
    flex-shrink: 0;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    min-width: 34px;

    &--pair {
      display: inline-flex;
      align-items: center;
      gap: @spacing-xs;
      min-width: 0;
    }
  }

  &__arrow { color: @text-tertiary; }
  &__tender { flex-shrink: 0; }
  &__name {
    flex: 0 1 auto;
    min-width: 0;
    max-width: 240px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;

    &--pending {
      color: @text-tertiary;
    }
  }
  &__wait { font-size: @font-size-xs; color: @text-tertiary; }
  &__spacer { flex: 1; }
  &__parse-bar {
    width: 72px;
    flex-shrink: 0;
    margin-inline-end: 0;
  }
  &__step {
    flex: 0 1 auto;
    min-width: 0;
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
  &__elapsed {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @brand-primary;
    font-variant-numeric: tabular-nums;
  }
  &__percent {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @text-secondary;
    font-variant-numeric: tabular-nums;
  }
  &__done-time {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @text-tertiary;
    font-variant-numeric: tabular-nums;
  }
  &__ok { color: @success; flex-shrink: 0; }
  &__bad { color: @danger; flex-shrink: 0; }
  &__pages { flex-shrink: 0; font-size: @font-size-xs; color: @text-tertiary; }
  &__sim { flex-shrink: 0; font-size: @font-size-xs; color: @text-secondary; }

  &__error {
    max-width: 280px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @danger;
  }

  &__status { flex-shrink: 0; }
}

.clause-edit {
  padding: @spacing-base @spacing-xl @spacing-xl;
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;

  &__row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
  }

  &__tag, &__source { flex-shrink: 0; }
  &__input { flex: 1; min-width: 0; }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-md;
    padding-top: @spacing-sm;
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.process-panel__skeleton {
  padding: @spacing-base @spacing-xl;
}

.process-feed {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-xl @spacing-xl;
}

.process-panel__ai-alert {
  margin: @spacing-base @spacing-xl 0;
}

@media (prefers-reduced-motion: reduce) {
  .trace-stage__chevron {
    transition: none;
  }
}
</style>
