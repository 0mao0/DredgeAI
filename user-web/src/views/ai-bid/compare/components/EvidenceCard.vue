<template>
  <div
    class="evidence-card"
    role="button"
    tabindex="0"
    @click="emit('trace', evidence)"
    @keydown.enter="emit('trace', evidence)"
  >
    <div class="evidence-card__head">
      <a-tag :color="SEVERITY_META[evidence.severity].color">{{ SEVERITY_META[evidence.severity].label }}</a-tag>
      <a-tag :color="EVIDENCE_TYPE_META[evidence.type].color">{{ EVIDENCE_TYPE_META[evidence.type].label }}</a-tag>
      <span class="evidence-card__spacer" />
      <span class="evidence-card__docs">
        <DocBadge
          v-for="id in evidence.docIds"
          :key="id"
          :label="docLabel(documents ?? [], id)"
        />
      </span>
    </div>
    <div class="evidence-card__title">{{ displayTitle }}</div>
    <div class="evidence-card__desc">{{ displaySummary }}</div>
    <div
      v-if="metricLines.length"
      class="evidence-card__metrics"
      :class="{ 'evidence-card__metrics--clickable': hasRefDetail }"
      @click.stop="hasRefDetail && (expanded = !expanded)"
    >
      <a-tag v-for="(line, i) in metricLines" :key="i" class="evidence-card__metric" color="blue">
        {{ line }}
      </a-tag>
    </div>

    <div v-if="expanded && hasRefDetail" class="evidence-card__refs">
      <div>
        <div v-if="refGroups.length && refGroups.length !== detailCount" class="evidence-card__ref-hint">
          已定位 {{ refGroups.length }} 组，其余暂无定位坐标
        </div>

        <div v-if="displayGroups.length" class="evidence-card__refs-list">
          <div
            v-for="group in displayGroups"
            :key="group.index"
            class="evidence-card__ref-group"
            role="button"
            tabindex="0"
            @click.stop="emit('traceRef', group.refs)"
            @keydown.enter.stop="emit('traceRef', group.refs)"
          >
            <span class="evidence-card__ref-index">#{{ group.index }}</span>
            <div class="evidence-card__ref-group-body">
              <div v-for="refItem in group.refs" :key="refItem.docId" class="evidence-card__ref-item">
                <span class="evidence-card__ref-meta">
                  <DocBadge :label="docLabel(documents ?? [], refItem.docId)" />
                  第 {{ refItem.page }} 页
                </span>
                <span class="evidence-card__ref-excerpt" :title="refItem.excerpt">{{ refItem.excerpt || '（无原文片段）' }}</span>
              </div>
              <div v-if="!group.refs.length" class="evidence-card__ref-empty">{{ group.text || '该处暂无原文片段与定位坐标' }}</div>
            </div>
          </div>
        </div>
        <div v-else class="evidence-card__ref-empty">暂无逐处定位明细</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { EVIDENCE_TYPE_META, SEVERITY_META, evidenceMetricLines } from '../evidenceMetrics'
import DocBadge from './DocBadge.vue'
import { docLabel } from '../constants'
import type { BlockRange, CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  evidence: EvidenceItem
  documents?: CompareDocMeta[]
}>()

const emit = defineEmits<{
  trace: [item: EvidenceItem]
  traceRef: [refs: BlockRange[]]
}>()

const metricLines = computed(() => evidenceMetricLines(props.evidence))
const displayTitle = computed(() => replaceDocIds(props.evidence.title, props.documents ?? []))
const displaySummary = computed(() => replaceDocIds(props.evidence.summary, props.documents ?? []))

function replaceDocIds(text: string, documents: CompareDocMeta[]): string {
  return text.replace(/\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, (id) => docLabel(documents, id))
}
const expanded = ref(false)
const hasRefDetail = computed(() =>
  props.evidence.refs.length > 0
  || typeof props.evidence.metrics?.sharedNgramCount === 'number'
  || typeof props.evidence.metrics?.matchedBlockCount === 'number',
)

const detailCount = computed(() => {
  const m = props.evidence.metrics
  if (typeof m?.sharedNgramCount === 'number') return m.sharedNgramCount
  if (typeof m?.matchedBlockCount === 'number') return m.matchedBlockCount
  return props.evidence.refs.length
})

const refGroups = computed(() => {
  const byDoc = new Map<string, BlockRange[]>()
  for (const ref of props.evidence.refs) {
    const list = byDoc.get(ref.docId) ?? []
    list.push(ref)
    byDoc.set(ref.docId, list)
  }
  const lists = props.evidence.docIds.map((id) => byDoc.get(id) ?? [])
  const max = Math.max(0, ...lists.map((list) => list.length))
  const groups: { index: number, refs: BlockRange[] }[] = []
  for (let i = 0; i < max; i++) {
    groups.push({
      index: i + 1,
      refs: lists.map((list) => list[i]).filter((ref): ref is BlockRange => Boolean(ref)),
    })
  }
  return groups
})

const displayGroups = computed(() => {
  const rawItems = props.evidence.metrics?.items
  if (Array.isArray(rawItems) && rawItems.length > 0) {
    return rawItems.map((raw, i) => {
      const item = raw as { index?: number, text?: string }
      const index = typeof item.index === 'number' ? item.index : i + 1
      const refs = props.evidence.refs.filter((r) => r.pairId === `${props.evidence.id}-${index}`)
      return { index, refs, text: typeof item.text === 'string' ? item.text : '' }
    })
  }
  const groups: { index: number, refs: BlockRange[], text?: string }[] = [...refGroups.value]
  for (let i = groups.length + 1; i <= detailCount.value; i++) {
    groups.push({ index: i, refs: [] })
  }
  return groups
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.evidence-card {
  padding: @spacing-md @spacing-xl;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @card-bg;
  cursor: pointer;
  transition: border-color @transition-fast, box-shadow @transition-fast;

  &:hover {
    border-color: @brand-primary;
    box-shadow: @shadow-sm;
  }

  &:focus-visible {
    outline: 2px solid @brand-primary;
    outline-offset: 2px;
  }

  &__head {
    display: flex;
    align-items: center;
    gap: @spacing-xs;
  }

  &__docs {
    display: inline-flex;
    align-items: center;
    gap: @spacing-xs;
  }

  &__spacer {
    flex: 1;
  }

  &__title {
    margin-top: @spacing-xs;
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
  }

  &__desc {
    margin-top: 2px;
    font-size: @font-size-xs;
    color: @text-secondary;
    line-height: 1.6;
  }

  &__metrics {
    margin-top: @spacing-sm;
    display: flex;
    flex-wrap: wrap;
    gap: @spacing-xs;
  }

    &__metrics--clickable {
      cursor: pointer;
      user-select: none;
    }

    &__refs {
      margin-top: @spacing-sm;
    }

    &__refs-toggle {
      padding: 0;
      height: auto;
      font-size: @font-size-xs;
    }

    &__refs-list {
      margin-top: @spacing-xs;
      max-height: 220px;
      overflow: auto;
      display: flex;
      flex-direction: column;
      gap: 4px;
      border-top: 1px dashed @divider-color;
      padding-top: @spacing-xs;
    }

    &__ref-item {
      display: flex;
      align-items: baseline;
      gap: @spacing-sm;
      font-size: @font-size-xs;
      line-height: 1.5;
    }

    &__ref-meta {
      display: inline-flex;
      align-items: center;
      gap: @spacing-xs;
      flex-shrink: 0;
      color: @text-tertiary;
      white-space: nowrap;
    }

    &__ref-excerpt {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      color: @text-secondary;
    }

    &__ref-group {
      display: flex;
      align-items: flex-start;
      gap: @spacing-sm;
      padding: 4px 6px;
      border-radius: @radius-base;
      cursor: pointer;
      transition: background @transition-fast;

      &:hover {
        background: color-mix(in srgb, @brand-primary 6%, @card-bg);
      }

      &:focus-visible {
        outline: 2px solid @brand-primary;
        outline-offset: 1px;
      }
    }

    &__ref-index {
      flex-shrink: 0;
      font-size: @font-size-xs;
      font-weight: @font-weight-medium;
      color: @text-tertiary;
      line-height: 20px;
    }

    &__ref-group-body {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    &__ref-hint {
      margin-top: @spacing-xs;
      font-size: @font-size-xs;
      color: @warning;
    }

    &__ref-empty {
      margin-top: @spacing-xs;
      font-size: @font-size-xs;
      color: @text-tertiary;
    }

}
</style>
