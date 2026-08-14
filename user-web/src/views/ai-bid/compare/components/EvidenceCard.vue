<template>
  <div class="evidence-card">
    <div class="evidence-card__head">
      <a-tag :color="SEVERITY_META[evidence.severity].color">{{ SEVERITY_META[evidence.severity].label }}</a-tag>
      <a-tag :color="EVIDENCE_TYPE_META[evidence.type].color">{{ EVIDENCE_TYPE_META[evidence.type].label }}</a-tag>
      <span class="evidence-card__docs">{{ docLabels }}</span>
      <span class="evidence-card__spacer" />
      <a-button type="link" size="small" @click="emit('trace', evidence)">
        <SearchOutlined />溯源
      </a-button>
    </div>
    <div class="evidence-card__title">{{ evidence.title }}</div>
    <div class="evidence-card__desc">{{ evidence.summary }}</div>
    <div v-if="metricLines.length" class="evidence-card__metrics">
      <a-tag v-for="(line, i) in metricLines" :key="i" class="evidence-card__metric" color="blue">
        {{ line }}
      </a-tag>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { SearchOutlined } from '@ant-design/icons-vue'
import { EVIDENCE_TYPE_META, SEVERITY_META, evidenceMetricLines } from '../evidenceMetrics'
import type { CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  evidence: EvidenceItem
  documents?: CompareDocMeta[]
}>()

const emit = defineEmits<{ trace: [item: EvidenceItem] }>()

const metricLines = computed(() => evidenceMetricLines(props.evidence))

const docLabels = computed(() => {
  const docs = props.documents ?? []
  return props.evidence.docIds.map((id) => {
    const idx = docs.findIndex((d) => d.id === id)
    return idx >= 0 ? String.fromCharCode(65 + idx) : id
  }).join(' / ')
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.evidence-card {
  padding: @spacing-md @spacing-xl;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @card-bg;

  &__head {
    display: flex;
    align-items: center;
    gap: @spacing-xs;
  }

  &__docs {
    font-size: @font-size-xs;
    color: @text-tertiary;
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
}
</style>
