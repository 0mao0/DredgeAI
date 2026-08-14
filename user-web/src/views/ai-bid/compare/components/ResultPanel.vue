<template>
  <div class="result-panel">
    <div class="result-panel__scroll">
      <!-- partial 失败条：结果正常 + 失败文档内联重试 -->
      <div v-if="failedDocs.length" class="result-panel__partial">
        <ExclamationCircleOutlined class="result-panel__partial-icon" />
        <div class="result-panel__partial-body">
          <span class="result-panel__partial-text">
            已跳过 {{ failedDocs.length }} 份失败文档，其余结果不受影响
          </span>
          <div v-for="d in failedDocs" :key="d.id" class="result-panel__partial-doc">
            <span>{{ docLabel(task.documents, d.id) }} · {{ d.fileName }}</span>
            <a-button type="link" size="small" @click="emit('reparseDoc', d.id)">重新解析</a-button>
          </div>
        </div>
        <a-button size="small" @click="emit('reparseAll')">全部重新解析</a-button>
      </div>

      <!-- 摘要卡 -->
      <SectionCard title="结果摘要" flush>
        <div class="result-summary">
          <div class="result-summary__metric">
            <span class="result-summary__value">{{ evidence.length }}</span>
            <span class="result-summary__label">发现总数</span>
          </div>
          <div class="result-summary__metric result-summary__metric--high">
            <span class="result-summary__value">{{ riskCounts.high }}</span>
            <span class="result-summary__label">高风险</span>
          </div>
          <div class="result-summary__metric result-summary__metric--mid">
            <span class="result-summary__value">{{ riskCounts.mid }}</span>
            <span class="result-summary__label">中风险</span>
          </div>
          <div class="result-summary__metric result-summary__metric--low">
            <span class="result-summary__value">{{ riskCounts.low }}</span>
            <span class="result-summary__label">低风险</span>
          </div>
          <div class="result-summary__metric">
            <span class="result-summary__value">{{ donePairCount }}/{{ pairCount }}</span>
            <span class="result-summary__label">完成比对对</span>
          </div>
          <a-button
            type="primary"
            size="small"
            :loading="exporting"
            @click="emit('export')"
          >
            <DownloadOutlined />导出报告
          </a-button>
        </div>
      </SectionCard>

      <!-- 筛选 + 分段发现 -->
      <div class="result-panel__filter">
        <a-segmented v-model:value="filter" :options="filterOptions" />
      </div>

      <div v-if="filtered.length" class="result-panel__feed">
        <div v-for="ev in filtered" :key="ev.id" class="result-item">
          <EvidenceCard :evidence="ev" :documents="task.documents" @trace="(e) => emit('locate', e)" />
          <div v-if="ev.docIds.length > 2" class="result-item__chips">
            <span class="result-item__chips-label">其余来源</span>
            <a-tag
              v-for="docId in ev.docIds.slice(2)"
              :key="docId"
              class="result-item__chip"
              @click="onChipClick(docId)"
            >
              {{ docLabel(task.documents, docId) }}
            </a-tag>
          </div>
        </div>
      </div>
      <a-empty v-else description="当前分类暂无发现" class="result-panel__empty" />

      <!-- 全局视图（默认展开） -->
      <div class="result-panel__global">
        <SimilarityHeatmap
          v-if="overview"
          :labels="overview.docLabels"
          :matrix="overview.simMatrix"
          :self-matrix="overview.simMatrixSelf"
          @cell-click="onHeatmapCell"
        />
        <ResponseMatrix :documents="task.documents" :evidence="evidence" @trace="(e) => emit('locate', e)" />
        <IndicatorTable :evidence="evidence" :documents="task.documents" @trace="(e) => emit('locate', e)" />
      </div>

      <!-- 过程记录（折叠） -->
      <a-collapse class="result-panel__log" :bordered="false">
        <a-collapse-panel key="log" header="过程记录">
          <div class="result-log">
            <div class="result-log__line">
              <a-tag color="blue">状态</a-tag>
              <span>{{ task.progress.message || '任务已完成' }}</span>
            </div>
            <div v-for="p in task.pairs ?? []" :key="p.pairId" class="result-log__line">
              <a-tag :color="PAIR_COLOR[p.status]">{{ PAIR_TEXT[p.status] }}</a-tag>
              <span>{{ docLabel(task.documents, p.docAId) }} ↔ {{ docLabel(task.documents, p.docBId) }}</span>
              <span v-if="p.similarity != null">相似度 {{ Math.round(p.similarity * 100) }}%</span>
              <span v-if="p.failReason">{{ p.failReason }}</span>
            </div>
            <div class="result-log__line">
              <a-tag color="default">证据</a-tag>
              <span>共 {{ evidence.length }} 条发现（含 AI 结论）</span>
            </div>
          </div>
        </a-collapse-panel>
      </a-collapse>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { message } from 'ant-design-vue'
import { DownloadOutlined, ExclamationCircleOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EvidenceCard from './EvidenceCard.vue'
import SimilarityHeatmap from './SimilarityHeatmap.vue'
import ResponseMatrix from './ResponseMatrix.vue'
import IndicatorTable from './IndicatorTable.vue'
import { docLabel } from '../constants'
import type { ComparePair, CompareTask, EvidenceItem, TaskOverview } from '@/types'

const props = defineProps<{
  task: CompareTask
  overview: TaskOverview | null
  evidence: EvidenceItem[]
  loading: boolean
  exporting: boolean
}>()

const emit = defineEmits<{
  locate: [item: EvidenceItem]
  locateDoc: [payload: { docId: string, page: number }]
  export: []
  reparseDoc: [docId: string]
  reparseAll: []
}>()

const PAIR_COLOR: Record<ComparePair['status'], string> = {
  waiting: 'default',
  processing: 'blue',
  done: 'green',
  failed: 'red',
}

const PAIR_TEXT: Record<ComparePair['status'], string> = {
  waiting: '等待',
  processing: '比对中',
  done: '完成',
  failed: '失败',
}

const filter = ref<'all' | 'collusion' | 'clause' | 'indicator'>('all')
const filterOptions = [
  { label: '全部', value: 'all' },
  { label: '串标', value: 'collusion' },
  { label: '条款', value: 'clause' },
  { label: '指标', value: 'indicator' },
]

const failedDocs = computed(() => props.task.documents.filter((d) => d.parseStatus === 'failed'))

const riskCounts = computed(() => ({
  high: props.evidence.filter((e) => e.severity === 'high').length,
  mid: props.evidence.filter((e) => e.severity === 'mid').length,
  low: props.evidence.filter((e) => e.severity === 'low').length,
}))

const pairs = computed(() => props.task.pairs ?? [])
const pairCount = computed(() => pairs.value.length)
const donePairCount = computed(() => pairs.value.filter((p) => p.status === 'done').length)

const filtered = computed(() => {
  if (filter.value === 'all') return props.evidence
  if (filter.value === 'collusion') {
    return props.evidence.filter((e) => e.type === 'similarity' || e.type === 'price' || e.type === 'metadata')
  }
  if (filter.value === 'clause') return props.evidence.filter((e) => e.type === 'clause')
  return props.evidence.filter((e) => e.type === 'indicator')
})

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

function onChipClick(docId: string): void {
  const ref = props.evidence.flatMap((e) => e.refs).find((r) => r.docId === docId)
  emit('locateDoc', { docId, page: ref?.page ?? 1 })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.result-panel {
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

.result-panel__scroll {
  height: 100%;
  overflow: auto;
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  padding: @spacing-md @spacing-base @spacing-xl;
}

.result-panel__partial {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border: 1px solid @warning;
  border-radius: @radius-base;
  background: color-mix(in srgb, @warning 8%, @card-bg);

  &-icon { color: @warning; }
  &-body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
  &-text { font-size: @font-size-xs; color: @text-secondary; }
  &-doc {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    font-size: @font-size-xs;
    color: @text-secondary;
  }
}

.result-summary {
  display: flex;
  align-items: center;
  gap: @spacing-xl;
  padding: @spacing-base @spacing-xl @spacing-xl;

  &__metric {
    display: flex;
    flex-direction: column;
    gap: 2px;

    &--high .result-summary__value { color: @danger; }
    &--mid .result-summary__value { color: @warning; }
    &--low .result-summary__value { color: @brand-primary; }
  }

  &__value {
    font-size: 24px;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    font-variant-numeric: tabular-nums;
  }

  &__label {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.result-panel__filter {
  flex-shrink: 0;
}

.result-panel__feed {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
}

.result-panel__empty {
  padding: @spacing-xl 0;
}

.result-item {
  &__chips {
    display: flex;
    align-items: center;
    gap: @spacing-xs;
    margin-top: @spacing-xs;
    padding-left: @spacing-md;
  }

  &__chips-label {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__chip {
    cursor: pointer;
    margin-inline-end: 0;
  }
}

.result-panel__global {
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
}

.result-panel__log {
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
}

.result-log {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;

  &__line {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    font-size: @font-size-xs;
    color: @text-secondary;
  }
}
</style>
