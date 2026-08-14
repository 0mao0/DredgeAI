<template>
  <div class="process-panel">
    <div class="process-panel__scroll">
      <!-- partial：结果正常 + 失败文档内联重试 -->
      <div v-if="failedDocs.length" class="process-panel__partial">
        <ExclamationCircleOutlined class="process-panel__partial-icon" />
        <span class="process-panel__partial-text">
          已跳过 {{ failedDocs.length }} 份失败文档，其余结果不受影响
        </span>
        <a-button size="small" :loading="reparseAllLoading" @click="emit('reparseAll')">
          重新解析失败文档
        </a-button>
      </div>

      <SectionCard title="处理进度" flush>
        <div class="process-stage">
          <div class="process-stage__head">
            <a-tag :color="stageColor" class="process-stage__tag">{{ stageText }}</a-tag>
            <span v-if="pairLabel" class="process-stage__pair">{{ pairLabel }}</span>
            <span class="process-stage__spacer" />
            <a-progress type="circle" :percent="overallPercent" :size="52" />
          </div>
          <p v-if="task.progress.message" class="process-stage__message">{{ task.progress.message }}</p>
        </div>
      </SectionCard>

      <SectionCard title="文档解析" flush>
        <div class="process-list">
          <div v-for="d in task.documents" :key="d.id" class="process-row">
            <span class="process-row__label">{{ docLabel(task.documents, d.id) }}</span>
            <a-tag v-if="d.role === 'tender'" class="process-row__tender">招标</a-tag>
            <span class="process-row__name" :title="d.fileName">{{ d.fileName }}</span>
            <template v-if="d.parseStatus === 'parsing'">
              <a-spin size="small" />
              <span class="process-row__parsing">解析中 · {{ elapsedText(d.id) }}</span>
            </template>
            <CheckCircleFilled v-else-if="d.parseStatus === 'done'" class="process-row__ok" />
            <CloseCircleFilled v-else-if="d.parseStatus === 'failed'" class="process-row__bad" />
            <span v-else class="process-row__wait">等待</span>
            <span v-if="d.pages" class="process-row__pages">{{ d.pages }} 页</span>
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
          </div>
          <a-empty v-if="!task.documents.length" description="暂无文档" />
        </div>
      </SectionCard>

      <!-- 条款确认：仅上传招标文件且尚未锁定快照时出现 -->
      <SectionCard v-if="showClauseConfirm" title="强制性条款确认" flush>
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
      </SectionCard>

      <SectionCard title="两两对比" flush>
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
      </SectionCard>

      <SectionCard v-if="showAiSection" title="AI 分析" flush>
        <a-alert
          v-if="aiUnavailable"
          type="warning"
          show-icon
          message="AI 分析暂不可用"
          description="算法证据不受影响，可稍后重试"
          class="process-panel__ai-alert"
        />
        <div v-else class="process-list">
          <div class="process-row">
            <a-spin size="small" />
            <span class="process-row__name">条款响应判定（{{ task.documents.filter((d) => d.role !== 'tender').length }} 份标书）</span>
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
      </SectionCard>

      <SectionCard title="发现流" flush>
        <div class="process-feed">
          <EvidenceCard
            v-for="ev in evidence"
            :key="ev.id"
            :evidence="ev"
            :documents="task.documents"
            @trace="(e) => emit('locate', e)"
          />
          <a-empty v-if="!evidence.length" description="暂无发现，比对开始后实时追加" />
        </div>
      </SectionCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import {
  CheckCircleFilled,
  CloseCircleFilled,
  DeleteOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
} from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EvidenceCard from './EvidenceCard.vue'
import { docLabel } from '../constants'
import type { ClauseItem, ComparePair, CompareTask, EvidenceItem } from '@/types'

const props = defineProps<{
  task: CompareTask
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

const parseStartedAt = ref<Record<string, number>>({})
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
  const parsingIds = docs.filter((d) => d.parseStatus === 'parsing').map((d) => d.id)
  if (!parsingIds.length) {
    parseStartedAt.value = {}
    stopParseTimer()
    return
  }
  const next: Record<string, number> = {}
  for (const id of parsingIds) {
    next[id] = parseStartedAt.value[id] ?? Date.now()
  }
  parseStartedAt.value = next
  startParseTimer()
}, { immediate: true })

onBeforeUnmount(stopParseTimer)

function elapsedText(docId: string): string {
  const start = parseStartedAt.value[docId]
  if (!start) return '0秒'
  const seconds = Math.max(0, Math.floor((nowTick.value - start) / 1000))
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return minutes > 0 ? `${minutes}分${rest}秒` : `${seconds}秒`
}

const PAIR_META: Record<ComparePair['status'], { color: string, text: string }> = {
  waiting: { color: 'default', text: '等待' },
  processing: { color: 'blue', text: '比对中' },
  done: { color: 'green', text: '完成' },
  failed: { color: 'red', text: '失败' },
}

const STAGE_META: Record<string, { color: string, text: string }> = {
  parsing: { color: 'blue', text: '解析文档' },
  clauses: { color: 'gold', text: '条款确认' },
  comparing: { color: 'blue', text: '两两对比' },
  analyzing: { color: 'purple', text: 'AI 分析' },
  done: { color: 'green', text: '已完成' },
}

const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))
const pairs = computed(() => props.task.pairs ?? [])
const stage = computed(() => STAGE_META[props.task.progress.stage ?? 'parsing'] ?? STAGE_META.parsing)
const stageColor = computed(() => stage.value.color)
const stageText = computed(() => stage.value.text)
const pairLabel = computed(() =>
  props.task.progress.pairIndex && props.task.progress.pairCount
    ? `第 ${props.task.progress.pairIndex}/${props.task.progress.pairCount} 对`
    : '',
)
const overallPercent = computed(() => {
  const p = props.task.progress
  if (p.stage === 'done') return 100
  if (p.stage === 'analyzing') return Math.min(99, 80 + Math.round((p.ai / 100) * 19))
  if (p.stage === 'comparing') return p.compare
  if (p.stage === 'clauses') return 40
  return p.parse
})
const showClauseConfirm = computed(() =>
  !!props.task.tenderDocId && !props.task.clauseSnapshot && !isTerminalish(props.task),
)
const aiUnavailable = computed(() => (props.task.progress.message ?? '').includes('AI 分析暂不可用'))
const showAiSection = computed(() => props.task.progress.stage === 'analyzing' || aiUnavailable.value)

function isTerminalish(t: CompareTask): boolean {
  return t.status === 'completed' || t.status === 'failed' || t.status === 'partial'
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

.process-stage {
  padding: @spacing-base @spacing-xl @spacing-xl;

  &__head {
    display: flex;
    align-items: center;
    gap: @spacing-md;
  }

  &__tag { margin-inline-end: 0; }
  &__pair {
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
  }
  &__spacer { flex: 1; }
  &__message {
    margin: @spacing-sm 0 0;
    font-size: @font-size-xs;
    color: @text-tertiary;
    line-height: 1.6;
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
  &__parsing {
    flex-shrink: 0;
    font-size: @font-size-xs;
    color: @brand-primary;
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
  padding: @spacing-base @spacing-xl @spacing-xl;
}

.process-panel__ai-alert {
  margin: @spacing-base @spacing-xl 0;
}

.process-panel__ai-retry {
  padding: @spacing-sm @spacing-xl @spacing-xl;
}
</style>
