<template>
  <div class="result-tabs">
    <a-tabs v-model:active-key="active" class="result-tabs__nav">
      <a-tab-pane key="requirements" tab="要求" />
      <a-tab-pane key="collusion" tab="串标" />
      <a-tab-pane key="similarity" tab="雷同" />
      <a-tab-pane key="response" tab="响应" />
      <a-tab-pane key="overall" tab="综合" />
      <template #rightExtra>
        <a-button
          type="primary"
          size="small"
          :disabled="!canExport"
          :loading="exporting"
          @click="emit('export')"
        >
          导出报告
        </a-button>
      </template>
    </a-tabs>

    <div class="result-tabs__body">
      <a-skeleton v-if="loading" active :paragraph="{ rows: 8 }" />
      <template v-else>
        <!-- 要求：招标文件提取结果查看/修改 -->
        <RequirementsPanel
          v-if="active === 'requirements'"
          :clauses="clauses"
          :saving="savingClauses"
          @save="(list) => emit('saveClauses', list)"
        />

        <!-- 串标：元数据一致性对比 -->
        <CollusionPanel
          v-else-if="active === 'collusion'"
          :documents="task.documents"
          :evidence="evidence"
          @locate="(ev) => emit('locate', ev)"
        />

        <!-- 雷同：两两文档相似度 + 雷同证据 -->
        <div v-else-if="active === 'similarity'" class="result-tabs__stack">
          <SimilarityHeatmap
            v-if="overview"
            :labels="overview.docLabels"
            :matrix="overview.simMatrix"
            :self-matrix="overview.simMatrixSelf"
            @cell-click="onHeatmapCell"
          />
          <EvidenceTable
            :evidence="simEvidence"
            :documents="task.documents"
            hide-type-filter
            clickable
            title="雷同证据"
            @jump="(ev) => emit('locate', ev)"
          />
        </div>

        <!-- 响应：针对招标文件的条款响应 -->
        <div v-else-if="active === 'response'" class="result-tabs__stack">
          <ResponseMatrix :task="task" />
          <EvidenceTable
            :evidence="clauseEvidence"
            :documents="task.documents"
            hide-type-filter
            clickable
            title="条款证据"
            @jump="(ev) => emit('locate', ev)"
          />
        </div>

        <!-- 综合：总体判断 -->
        <div v-else class="result-tabs__stack">
          <MetricCardRow
            :evidence="evidence"
            :doc-count="task.documents.length"
            :risk-summary="task.riskSummary"
          />
          <SectionCard title="综合结论" flush>
            <div class="overall-conclusion">
              <a-tag :color="conclusionColor" class="overall-conclusion__tag">{{ conclusionLevel }}</a-tag>
              <p class="overall-conclusion__text">{{ conclusionText }}</p>
            </div>
          </SectionCard>
          <MetricsTable :documents="task.documents" />
          <IntegrityTable
            :documents="task.documents"
            @jump="(docId, page) => emit('locateDoc', { docId, page })"
          />
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import RequirementsPanel from './RequirementsPanel.vue'
import CollusionPanel from './CollusionPanel.vue'
import SimilarityHeatmap from './SimilarityHeatmap.vue'
import EvidenceTable from './EvidenceTable.vue'
import ResponseMatrix from './ResponseMatrix.vue'
import MetricCardRow from './MetricCardRow.vue'
import MetricsTable from './MetricsTable.vue'
import IntegrityTable from './IntegrityTable.vue'
import type { ClauseItem, CompareTask, EvidenceItem, TaskOverview } from '@/types'

const props = defineProps<{
  task: CompareTask
  overview: TaskOverview | null
  evidence: EvidenceItem[]
  clauses: ClauseItem[]
  loading?: boolean
  savingClauses?: boolean
  exporting?: boolean
}>()

const emit = defineEmits<{
  locate: [item: EvidenceItem]
  locateDoc: [payload: { docId: string, page: number }]
  saveClauses: [list: ClauseItem[]]
  export: []
}>()

const active = ref('similarity')

const canExport = computed(() => !!props.overview)

const simEvidence = computed(() => props.evidence.filter((e) => e.type === 'similarity'))
const clauseEvidence = computed(() => props.evidence.filter((e) => e.type === 'clause'))

const riskCounts = computed(() => {
  const summary = props.task.riskSummary
  return {
    high: summary?.high ?? props.evidence.filter((e) => e.severity === 'high').length,
    mid: summary?.mid ?? props.evidence.filter((e) => e.severity === 'mid').length,
    low: summary?.low ?? props.evidence.filter((e) => e.severity === 'low').length,
  }
})

const conclusionLevel = computed(() =>
  riskCounts.value.high > 0 ? '围标串标高风险' : riskCounts.value.mid > 0 ? '存在疑点，建议复核' : '未见明显异常',
)

const conclusionColor = computed(() =>
  riskCounts.value.high > 0 ? '#EF4444' : riskCounts.value.mid > 0 ? '#F59E0B' : '#3B82F6',
)

const conclusionText = computed(() => {
  const pairs = props.overview?.pairs ?? []
  const top = pairs.reduce((m, p) => (p.overall > m.overall ? p : m), pairs[0] ?? { docA: '', docB: '', overall: 0 })
  const label = (id: string) => {
    const idx = props.task.documents.findIndex((d) => d.id === id)
    return idx >= 0 ? String.fromCharCode(65 + idx) : id
  }
  const parts: string[] = []
  if (top.docA) {
    parts.push(`${label(top.docA)} 与 ${label(top.docB)} 总体相似度最高（${Math.round(top.overall * 100)}%）`)
  }
  parts.push(`高风险证据 ${riskCounts.value.high} 条、中风险 ${riskCounts.value.mid} 条、低风险 ${riskCounts.value.low} 条`)
  if (props.task.riskSummary?.clauseMissing) {
    parts.push(`${props.task.riskSummary.clauseMissing} 项强制条款存在不响应`)
  }
  parts.push('建议结合串标元数据与雷同证据综合判定，并对高风险条目逐一溯源核查')
  return `${parts.join('；')}。`
})

function onHeatmapCell(pair: { docA: string, docB: string }): void {
  const labels = props.overview?.docLabels ?? []
  const docs = props.task.documents
  const docAId = docs[labels.indexOf(pair.docA)]?.id
  const docBId = docs[labels.indexOf(pair.docB)]?.id
  const ev = props.evidence.find((e) => docAId && docBId && e.docIds.includes(docAId) && e.docIds.includes(docBId))
  if (ev) {
    emit('locate', ev)
  } else {
    message.info('该文档对暂无证据')
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.result-tabs {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  padding: @spacing-md @spacing-base @spacing-base;
  overflow: hidden;

  :deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; }
  :deep(.ant-tabs-tab) { padding: 6px 10px; }
}

.result-tabs__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.result-tabs__stack {
  display: flex;
  flex-direction: column;
  gap: @spacing-xl;
}

.overall-conclusion {
  padding: @spacing-base @spacing-xl @spacing-xl;

  &__tag {
    margin-bottom: @spacing-sm;
  }

  &__text {
    margin: 0;
    font-size: @font-size-sm;
    color: @text-secondary;
    line-height: 1.7;
  }
}
</style>
