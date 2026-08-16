<template>
  <div class="risk-panel">
    <SectionCard title="雷同分析" flush>
      <div class="risk-panel__heatmap">
        <SimilarityHeatmap
          v-if="overview"
          :labels="overview.docLabels"
          :matrix="overview.simMatrix"
          :self-matrix="overview.simMatrixSelf"
          @cell-click="(pair) => emit('locatePair', pair)"
        />
      </div>
      <EvidenceTable
        :evidence="simEvidence"
        :documents="documents"
        hide-type-filter
        clickable
        title="雷同证据"
        @jump="(ev) => emit('locate', ev)"
        @trace="(ev) => emit('trace', ev)"
      />
    </SectionCard>

    <SectionCard title="报价规律" flush>
      <div v-if="priceEvidence.length" class="risk-panel__list">
        <EvidenceCard
          v-for="ev in priceEvidence"
          :key="ev.id"
          :evidence="ev"
          :documents="documents"
          @trace="(e) => emit('trace', e)"
        />
      </div>
      <a-empty v-else description="未发现报价规律异常" />
    </SectionCard>

    <SectionCard title="属性信息痕迹" flush>
      <div v-if="metaEvidence.length" class="risk-panel__list">
        <EvidenceCard
          v-for="ev in metaEvidence"
          :key="ev.id"
          :evidence="ev"
          :documents="documents"
          @trace="(e) => emit('trace', e)"
        />
      </div>
      <a-empty v-else description="未发现属性信息同源痕迹" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import SimilarityHeatmap from './SimilarityHeatmap.vue'
import EvidenceTable from './EvidenceTable.vue'
import EvidenceCard from './EvidenceCard.vue'
import type { CompareDocMeta, EvidenceItem, TaskOverview } from '@/types'

const props = defineProps<{
  overview: TaskOverview | null
  evidence: EvidenceItem[]
  documents: CompareDocMeta[]
}>()

const emit = defineEmits<{
  trace: [item: EvidenceItem]
  locate: [item: EvidenceItem]
  locatePair: [pair: { docA: string, docB: string }]
}>()

const simEvidence = computed(() => props.evidence.filter((e) => e.type === 'similarity'))
const priceEvidence = computed(() => props.evidence.filter((e) => e.type === 'price'))
const metaEvidence = computed(() => props.evidence.filter((e) => e.type === 'metadata'))
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.risk-panel {
  display: flex;
  flex-direction: column;
  gap: @spacing-xl;
}

.risk-panel__heatmap {
  padding: @spacing-md @spacing-xl;
}

.risk-panel__list {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
  padding: @spacing-base @spacing-xl @spacing-xl;
}
</style>
