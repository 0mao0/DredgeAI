<template>
  <div class="process-panel">
    <div class="process-panel__scroll">
      <div v-if="failedDocs.length" class="process-panel__partial">
        <ExclamationCircleOutlined class="process-panel__partial-icon" />
        <span class="process-panel__partial-text">
          已跳过 {{ failedDocs.length }} 份失败文档，其余结果不受影响
        </span>
        <a-button size="small" :loading="reparseAllLoading" @click="emit('reparseAll')">
          重新解析失败文档
        </a-button>
      </div>

      <section
        v-for="(stage, index) in visibleStages"
        :key="stage.key"
        class="trace-stage"
        :class="{ 'trace-stage--collapsed': isCollapsed(stage.key) }"
      >
        <button
          type="button"
          class="trace-stage__head"
          :class="{ 'trace-stage__head--active': !isStageDone(stage.key) }"
          @click="toggleStage(stage.key)"
        >
          <span class="trace-stage__index">{{ index + 1 }}</span>
          <span class="trace-stage__title">{{ stage.title }}</span>
          <span class="trace-stage__summary">{{ summaryOf(stage.key) }}</span>
          <span class="trace-stage__spacer" />
          <a-tag :color="metaOf(stage.key).color" class="trace-stage__tag">{{ metaOf(stage.key).text }}</a-tag>
          <DownOutlined
            v-if="isStageDone(stage.key)"
            class="trace-stage__chevron"
            :class="{ 'trace-stage__chevron--collapsed': isCollapsed(stage.key) }"
          />
        </button>

        <div v-show="!isCollapsed(stage.key)" class="trace-stage__body">
          <template v-if="stage.key === 'parse'">
            <div class="process-list">
              <div v-for="d in task.documents" :key="d.id" class="process-row">
                <span class="process-row__label">{{ docLabel(task.documents, d.id) }}</span>
                <a-tag v-if="d.role === 'tender'" class="process-row__tender">招标</a-tag>
                <template v-if="d.parseStatus === 'parsing'">
                  <div class="process-row__parse">
                    <div class="process-row__parse-head">
                      <a-spin size="small" />
                      <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
                      <span class="process-row__elapsed">解析中 · {{ elapsedText(d) }}</span>
                    </div>
                    <div class="process-row__parse-meta">
                      <a-progress
                        class="process-row__parse-bar"
                        :percent="d.parseProgress ?? 0"
                        :show-info="false"
                        size="small"
                      />
                      <span class="process-row__step" :title="stepText(d)">{{ stepText(d) }}</span>
                      <span v-if="d.parseProgress != null" class="process-row__percent">{{ d.parseProgress }}%</span>
                    </div>
                  </div>
                </template>
                <template v-else>
                  <CheckCircleFilled v-if="d.parseStatus === 'done'" class="process-row__ok" />
                  <CloseCircleFilled v-else-if="d.parseStatus === 'failed'" class="process-row__bad" />
                  <span v-else class="process-row__wait">等待</span>
                  <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
                  <span v-if="d.pages" class="process-row__pages">{{ d.pages }} 页</span>
                  <span v-if="parseDurationText(d)" class="process-row__done-time">
                    解析耗时 {{ parseDurationText(d) }}
                  </span>
                  <span v-if="d.failReason" class="process-row__error" :title="d.failReason">{{ d.failReason }}</span>
                  <a-button
                    v-if="d.parseStatus === 'failed'"
                    type="link"
                    size="small"
                    :loading="reparseDocIds.includes(d.id)"
                    @click="emit('reparseDoc', d.id)"
                  >
                    重新解析
                  </a-button>
                </template>
              </div>
              <a-empty v-if="!task.documents.length" description="暂无文档" />
            </div>
          </template>

          <template v-else-if="stage.key === 'clause'">
            <div v-if="extracting" class="process-panel__skeleton">
              <a-skeleton active :paragraph="{ rows: 3 }" />
            </div>
            <a-empty v-else-if="!editableDrafts.length" description="尚未提取条款">
              <a-button type="primary" size="small" @click="emit('extractClauses')">提取条款</a-button>
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
                <a-button type="text" size="small" @click="removeClause(i)">
                  <DeleteOutlined />
                </a-button>
              </div>
              <a-button size="small" type="dashed" block @click="addClause">
                <PlusOutlined />添加条款
              </a-button>
              <div class="clause-edit__footer">
                <span class="clause-edit__hint">确认后锁定任务快照，进入两两对比</span>
                <a-button
                  type="primary"
                  :loading="confirmingClauses"
                  :disabled="!editableDrafts.length"
                  @click="emit('confirmClauses', editableDrafts.map(toPayload))"
                >
                  确认并继续
                </a-button>
              </div>
            </div>
          </template>

          <template v-else-if="stage.key === 'compare'">
            <div class="process-list">
              <div v-for="p in pairs" :key="p.pairId" class="process-row">
                <span class="process-row__label">
                  {{ docLabel(task.documents, p.docAId) }} ↔ {{ docLabel(task.documents, p.docBId) }}
                </span>
                <a-tag :color="PAIR_META[p.status].color" class="process-row__status">{{ PAIR_META[p.status].text }}</a-tag>
                <span v-if="p.similarity != null" class="process-row__sim">
                  相似度 {{ Math.round(p.similarity * 100) }}%
                </span>
                <span v-if="p.failReason" class="process-row__error" :title="p.failReason">{{ p.failReason }}</span>
                <a-button
                  v-if="p.status === 'failed'"
                  type="link"
                  size="small"
                  :loading="retryingPairIds.includes(p.pairId)"
                  @click="emit('retryPair', p.pairId)"
                >
                  重试该对
                </a-button>
              </div>
              <a-empty v-if="!pairs.length" description="比对对将在解析完成后生成" />
            </div>

            <template v-if="compareEvidence.length">
              <div class="trace-stage__subtitle">串标查重发现</div>
              <div class="process-feed">
                <EvidenceCard
                  v-for="ev in compareEvidence"
                  :key="ev.id"
                  :evidence="ev"
                  :documents="task.documents"
                  @trace="(e) => emit('locate', e)"
                />
              </div>
            </template>

            <SimilarityHeatmap
              v-if="overview && overview.docLabels.length"
              :labels="overview.docLabels"
              :matrix="overview.simMatrix"
              :self-matrix="overview.simMatrixSelf"
              @cell-click="onHeatmapCell"
            />
          </template>

          <template v-else>
            <div v-if="aiUnavailable" class="process-panel__ai-alert">
              <a-alert
                type="warning"
                show-icon
                message="AI 分析暂不可用"
                description="算法证据不受影响，可稍后重试"
              />
            </div>
            <div v-else-if="!aiDone" class="process-list">
              <div class="process-row">
                <a-spin size="small" />
                <span class="process-row__name">条款响应判定（{{ bidCount }} 份标书）</span>
              </div>
              <div class="process-row">
                <a-spin size="small" />
                <span class="process-row__name">关键指标抽取</span>
              </div>
              <div class="process-row">
                <a-spin size="small" />
                <span class="process-row__name">AI 综合结论生成</span>
              </div>
            </div>
            <div v-if="aiUnavailable" class="process-panel__ai-retry">
              <a-button size="small" :loading="retryingCompare" @click="emit('retryCompare')">重试 AI</a-button>
            </div>

            <template v-if="aiEvidence.length">
              <div class="trace-stage__subtitle">条款与指标发现</div>
              <div class="process-feed">
                <EvidenceCard
                  v-for="ev in aiEvidence"
                  :key="ev.id"
                  :evidence="ev"
                  :documents="task.documents"
                  @trace="(e) => emit('locate', e)"
                />
              </div>
            </template>

            <ResponseMatrix
              :documents="task.documents"
              :evidence="evidence"
              @trace="(e) => emit('locate', e)"
            />
            <IndicatorTable
              :evidence="evidence"
              :documents="task.documents"
              @trace="(e) => emit('locate', e)"
            />
          </template>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
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
import ResponseMatrix from './ResponseMatrix.vue'
import IndicatorTable from './IndicatorTable.vue'
import { docLabel } from '../constants'
import type { ClauseItem, CompareDocMeta, ComparePair, CompareTask, EvidenceItem, TaskOverview } from '@/types'

type StageKey = 'parse' | 'clause' | 'compare' | 'ai'

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
}>()

const emit = defineEmits<{
  reparseDoc: [docId: string]
  reparseAll: []
  retryPair: [pairId: string]
  retryCompare: []
  extractClauses: []
  confirmClauses: [clauses: ClauseItem[]]
  locate: [item: EvidenceItem]
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

watch(() => props.task.documents, (docs) => {
  const parsing = docs.some((d) => d.parseStatus === 'parsing')
  if (parsing) {
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

function elapsedText(d: CompareDocMeta): string {
  const start = d.parseStartedAt ? Date.parse(d.parseStartedAt) : undefined
  if (!start) return '0秒'
  const end = d.parseFinishedAt ? Date.parse(d.parseFinishedAt) : nowTick.value
  return formatSeconds((end - start) / 1000)
}

function parseDurationText(d: CompareDocMeta): string {
  if (d.parseStatus !== 'done' && d.parseStatus !== 'failed') return ''
  if (!d.parseStartedAt || !d.parseFinishedAt) return ''
  return formatSeconds((Date.parse(d.parseFinishedAt) - Date.parse(d.parseStartedAt)) / 1000)
}

function stepText(d: CompareDocMeta): string {
  const parts = [d.parseStage, d.parseStageMessage].filter((s): s is string => !!s)
  return parts.length ? parts.join(' · ') : '解析中'
}

const PAIR_META: Record<ComparePair['status'], { color: string, text: string }> = {
  waiting: { color: 'default', text: '等待' },
  processing: { color: 'blue', text: '比对中' },
  done: { color: 'green', text: '完成' },
  failed: { color: 'red', text: '失败' },
}

const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))
const pairs = computed(() => props.task.pairs ?? [])
const failedPairs = computed(() => pairs.value.filter((p) => p.status === 'failed'))
const bidCount = computed(() => props.task.documents.filter((d) => d.role !== 'tender').length)
const compareEvidence = computed(() =>
  props.evidence.filter((e) => e.type === 'similarity' || e.type === 'price' || e.type === 'metadata'))
const aiEvidence = computed(() =>
  props.evidence.filter((e) => e.type === 'clause' || e.type === 'indicator'))
const aiUnavailable = computed(() => (props.task.progress.message ?? '').includes('AI 分析暂不可用'))

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
  && (props.task.progress.stage === 'analyzing'
    || props.task.progress.stage === 'done'
    || isTerminalish(props.task)),
)
const aiDone = computed(() => isTerminalish(props.task) || props.task.progress.stage === 'done')

const visibleStages = computed<{ key: StageKey, title: string }[]>(() => {
  const list: { key: StageKey, title: string }[] = [{ key: 'parse', title: '文档解析' }]
  if (clauseVisible.value) list.push({ key: 'clause', title: '条款确认' })
  if (compareVisible.value) list.push({ key: 'compare', title: '两两对比' })
  if (aiVisible.value) list.push({ key: 'ai', title: 'AI 分析' })
  return list
})

const stageDoneMap = computed<Record<StageKey, boolean>>(() => ({
  parse: parseDone.value,
  clause: clauseDone.value,
  compare: compareDone.value,
  ai: aiDone.value,
}))

function isStageDone(key: StageKey): boolean {
  return stageDoneMap.value[key]
}

const expandedStages = ref<Set<StageKey>>(new Set())

function isCollapsed(key: StageKey): boolean {
  if (!isStageDone(key)) return false
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

const parseSummary = computed(() => {
  if (!parseDone.value) {
    const parsing = props.task.documents.filter((d) => d.parseStatus === 'parsing').length
    return parsing ? `解析中 ${parsing}/${props.task.documents.length}` : '等待解析'
  }
  const pages = props.task.documents.reduce((acc, d) => acc + (d.pages || 0), 0)
  const failed = failedDocs.value.length
  return `文档解析完成 · ${props.task.documents.length} 份 · ${pages} 页${failed ? ` · ${failed} 份失败` : ''}`
})

const clauseCount = computed(() => props.task.clauseSnapshot?.length ?? 0)
const clauseSummary = computed(() =>
  clauseDone.value ? `条款确认完成 · ${clauseCount.value} 条` : '等待确认')

const compareSummary = computed(() => {
  if (!compareDone.value) {
    const processing = pairs.value.find((p) => p.status === 'processing')
    if (processing) {
      const fallbackIndex = pairs.value.findIndex((p) => p.pairId === processing.pairId) + 1
      const idx = props.task.progress.pairIndex ?? (fallbackIndex > 0 ? fallbackIndex : 1)
      return `第 ${idx}/${pairs.value.length || '—'} 对比对中`
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

const aiSummary = computed(() =>
  aiDone.value ? `AI 分析完成 · 共 ${aiEvidence.value.length} 条发现` : 'AI 分析中')

function summaryOf(key: StageKey): string {
  switch (key) {
    case 'parse': return parseSummary.value
    case 'clause': return clauseSummary.value
    case 'compare': return compareSummary.value
    case 'ai': return aiSummary.value
  }
}

interface StageMeta {
  color: string
  text: string
}

const stageMeta = computed<Record<StageKey, StageMeta>>(() => ({
  parse: parseDone.value
    ? { color: failedDocs.value.length ? 'orange' : 'green', text: failedDocs.value.length ? '部分完成' : '完成' }
    : {
      color: props.task.documents.some((d) => d.parseStatus === 'parsing') ? 'blue' : 'default',
      text: props.task.documents.some((d) => d.parseStatus === 'parsing') ? '解析中' : '等待',
    },
  clause: clauseDone.value ? { color: 'green', text: '完成' } : { color: 'gold', text: '待确认' },
  compare: compareDone.value
    ? { color: failedPairs.value.length ? 'orange' : 'green', text: failedPairs.value.length ? '部分完成' : '完成' }
    : {
      color: pairs.value.some((p) => p.status === 'processing') ? 'blue' : 'default',
      text: pairs.value.some((p) => p.status === 'processing') ? '比对中' : '等待',
    },
  ai: aiDone.value ? { color: 'green', text: '完成' } : { color: 'purple', text: '分析中' },
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

.process-panel__partial {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border: 1px solid @warning;
  border-radius: @radius-base;
  background: color-mix(in srgb, @warning 8%, @card-bg);

  &-icon { color: @warning; }
  &-text { flex: 1; min-width: 0; font-size: @font-size-xs; color: @text-secondary; }
}

.trace-stage {
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  background: @card-bg;
  overflow: hidden;

  &__head {
    width: 100%;
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
    padding: @spacing-sm @spacing-xl 0;
    font-size: @font-size-xs;
    font-weight: @font-weight-medium;
    color: @text-tertiary;
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
  }

  &__tender { flex-shrink: 0; }
  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }
  &__wait { font-size: @font-size-xs; color: @text-tertiary; }
  &__parse {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 3px;
  }
  &__parse-head {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    min-width: 0;
  }
  &__parse-meta {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    padding-left: 22px;
    min-width: 0;
  }
  &__parse-bar {
    width: 72px;
    flex-shrink: 0;
    margin-inline-end: 0;
  }
  &__step {
    flex: 1;
    min-width: 0;
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

.process-panel__ai-retry {
  padding: @spacing-sm @spacing-xl @spacing-xl;
}

@media (prefers-reduced-motion: reduce) {
  .trace-stage__chevron {
    transition: none;
  }
}
</style>
