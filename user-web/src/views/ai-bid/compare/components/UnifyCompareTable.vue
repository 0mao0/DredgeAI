<template>
  <div class="unify-compare">
    <a-table
      size="small"
      :columns="columns"
      :data-source="rows"
      :pagination="false"
      row-key="__key"
      :scroll="{ x: 940 }"
      :custom-row="customRow"
      :locale="{ emptyText: '暂无雷同/查重结果' }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'kind'">
          <a-tag v-if="record.kind === 'passage'" color="blue">片段</a-tag>
          <a-tag v-else :color="typeColor(record.evidence.type)">{{ typeText(record.evidence.type) }}</a-tag>
        </template>

        <template v-else-if="column.key === 'content'">
          <a-tooltip v-if="record.kind === 'passage'" :title="record.content" placement="topLeft">
            <span class="unify-compare__text">{{ record.content }}</span>
          </a-tooltip>
          <div v-else class="unify-compare__evidence">
            <div class="unify-compare__title">{{ record.content }}</div>
            <div class="unify-compare__summary">{{ record.detail }}</div>
          </div>
        </template>

        <template v-else-if="column.key === 'metric'">
          <span v-if="record.kind === 'passage'" class="unify-compare__metric">{{ record.length }}</span>
          <span v-else class="unify-compare__metric">{{ record.metric }}</span>
        </template>

        <template v-else-if="column.key === 'location'">
          <span class="unify-compare__loc">
            <DocBadge :label="docLabel(documents, record.docAId)" />
            <span class="unify-compare__pages">{{ joinPages(record.pagesA) }}</span>
            <span class="unify-compare__arrow">↔</span>
            <DocBadge :label="docLabel(documents, record.docBId)" />
            <span class="unify-compare__pages">{{ joinPages(record.pagesB) }}</span>
          </span>
        </template>

        <template v-else-if="column.key === 'tag'">
          <a-tag v-if="record.kind === 'passage'" :color="record.tagColor">{{ record.tagText }}</a-tag>
          <a-tag v-else :color="record.tagColor">{{ record.tagText }}</a-tag>
        </template>

        <template v-else-if="column.key === 'action'">
          <AppButton
            variant="link"
            size="sm"
            :loading="locatingKeys.has(record.__key)"
            @click.stop="locate(record)"
          >
            定位
          </AppButton>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { message } from 'ant-design-vue'
import { AppButton } from '@shared/web'
import { getBlockRefs } from '@/api/modules/compare'
import { evidenceMetricLines } from '../evidenceMetrics'
import DocBadge from './DocBadge.vue'
import { docLabel } from '../constants'
import type { BlockRange, CompareDocMeta, EvidenceItem, EvidenceType, RiskLevel } from '@/types'

interface PassageItem {
  text: string
  length: number
  docA: { blockIds: string[], pages: number[] }
  docB: { blockIds: string[], pages: number[] }
  tenderResponse?: boolean | null
  tenderRatio?: number
}

interface CompareRow {
  __key: string
  index: number
  kind: 'passage' | 'evidence'
  content: string
  detail: string
  length?: number
  metric?: string
  docAId: string
  docBId: string
  pagesA: number[]
  pagesB: number[]
  tagText: string
  tagColor: string
  passage?: PassageItem
  evidence?: EvidenceItem
}

const props = defineProps<{
  passageEvidence: EvidenceItem[]
  compareEvidence: EvidenceItem[]
  documents: CompareDocMeta[]
  taskId: string
}>()

const emit = defineEmits<{
  locate: [item: EvidenceItem]
  locateRefs: [refs: BlockRange[]]
}>()

const columns = [
  { title: '序号', dataIndex: 'index', key: 'index', width: 56 },
  { title: '类型', key: 'kind', width: 90 },
  { title: '内容', key: 'content' },
  { title: '字数/指标', key: 'metric', width: 100 },
  { title: '位置', key: 'location', width: 210 },
  { title: '判定/严重度', key: 'tag', width: 120 },
  { title: '操作', key: 'action', width: 80 },
]

const locatingKeys = ref<Set<string>>(new Set())
const refsCache = new Map<string, Promise<BlockRange[]>>()

function cached(key: string, load: () => Promise<BlockRange[]>): Promise<BlockRange[]> {
  const hit = refsCache.get(key)
  if (hit) return hit
  const p = load()
  refsCache.set(key, p)
  return p
}

function passageRows(): CompareRow[] {
  const rows: CompareRow[] = []
  for (const ev of props.passageEvidence) {
    const items = (ev.metrics?.passages as PassageItem[] | undefined) ?? []
    for (const p of items) {
      const isTender = p.tenderResponse
      rows.push({
        __key: `p-${ev.id}-${rows.length}`,
        index: 0,
        kind: 'passage',
        content: p.text,
        detail: '',
        length: p.length,
        docAId: ev.docIds[0],
        docBId: ev.docIds[1],
        pagesA: p.docA.pages ?? [],
        pagesB: p.docB.pages ?? [],
        tagText: isTender === true ? '招标响应' : isTender === false ? '雷同候选' : '—',
        tagColor: isTender === true ? 'green' : isTender === false ? 'red' : 'default',
        passage: p,
      })
    }
  }
  return rows.sort((a, b) => (b.length ?? 0) - (a.length ?? 0))
}

const SEVERITY_ORDER: Record<RiskLevel, number> = { high: 0, mid: 1, low: 2 }

function evidenceRows(): CompareRow[] {
  return [...props.compareEvidence]
    .sort((a, b) => SEVERITY_ORDER[a.severity] - SEVERITY_ORDER[b.severity])
    .map((ev) => ({
      __key: `e-${ev.id}`,
      index: 0,
      kind: 'evidence' as const,
      content: ev.title,
      detail: ev.summary,
      metric: evidenceMetricLines(ev).join(' · '),
      docAId: ev.docIds[0],
      docBId: ev.docIds[1] ?? ev.docIds[0],
      pagesA: pagesOfRefs(ev, ev.docIds[0]),
      pagesB: pagesOfRefs(ev, ev.docIds[1] ?? ev.docIds[0]),
      tagText: severityText(ev.severity),
      tagColor: severityColor(ev.severity),
      evidence: ev,
    }))
}

function pagesOfRefs(ev: EvidenceItem, docId: string): number[] {
  return [...new Set(ev.refs.filter((r) => r.docId === docId).map((r) => r.page))].sort((a, b) => a - b)
}

const rows = computed<CompareRow[]>(() => {
  const evidences = evidenceRows()
  const passages = passageRows()
  return [...evidences, ...passages].map((r, i) => ({ ...r, index: i + 1 }))
})

function customRow(record: CompareRow) {
  return {
    onClick: () => locate(record),
  }
}

async function locate(row: CompareRow): Promise<void> {
  if (row.kind === 'evidence' && row.evidence) {
    emit('locate', row.evidence)
    return
  }
  if (row.kind !== 'passage' || !row.passage) return

  const p = row.passage
  const keyA = `${row.docAId}:${p.docA.blockIds.join(',')}`
  const keyB = `${row.docBId}:${p.docB.blockIds.join(',')}`
  locatingKeys.value = new Set([...locatingKeys.value, row.__key])
  try {
    const [refsA, refsB] = await Promise.all([
      cached(keyA, () => getBlockRefs(props.taskId, row.docAId, p.docA.blockIds)),
      cached(keyB, () => getBlockRefs(props.taskId, row.docBId, p.docB.blockIds)),
    ])
    const all = [...refsA, ...refsB]
    if (all.length) {
      emit('locateRefs', all)
    } else {
      message.info('该片段缺少可定位坐标')
    }
  } finally {
    const next = new Set(locatingKeys.value)
    next.delete(row.__key)
    locatingKeys.value = next
  }
}

function joinPages(pages: number[]): string {
  return pages.length ? `第 ${pages.join('/')} 页` : ''
}

function severityText(s: RiskLevel): string {
  return s === 'high' ? '高风险' : s === 'mid' ? '中风险' : '低风险'
}

function severityColor(s: RiskLevel): string {
  return s === 'high' ? 'red' : s === 'mid' ? 'orange' : 'blue'
}

function typeColor(t: EvidenceType): string {
  const map: Record<EvidenceType, string> = {
    similarity: 'red',
    price: 'orange',
    metadata: 'blue',
    clause: 'green',
    indicator: 'purple',
  }
  return map[t]
}

function typeText(t: EvidenceType): string {
  const map: Record<EvidenceType, string> = {
    similarity: '雷同',
    price: '报价',
    metadata: '属性信息',
    clause: '条款',
    indicator: '指标',
  }
  return map[t]
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.unify-compare {
  padding: @spacing-xs @spacing-xl @spacing-md;

  &__text {
    display: block;
    max-width: 280px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @text-primary;
  }

  &__title {
    font-size: @font-size-xs;
    font-weight: @font-weight-medium;
    color: @text-primary;
    text-align: left;
  }

  &__summary {
    font-size: @font-size-xs;
    color: @text-tertiary;
    text-align: left;
    margin-top: 2px;
    line-height: 1.5;
  }

  &__metric {
    font-size: @font-size-xs;
    color: @text-secondary;
  }

  &__loc {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    font-size: @font-size-xs;
    color: @text-secondary;
    white-space: nowrap;
  }

  &__pages {
    white-space: nowrap;
  }

  &__arrow {
    color: @text-tertiary;
  }
}
</style>
