<template>
  <div class="pdf-workspace">
    <div class="pdf-workspace__bar">
      <div v-if="singlePane" class="pdf-workspace__tabs-single">
        <a-tabs
          v-model:active-key="leftDocId"
          size="small"
          class="pdf-workspace__tabs"
          :animated="false"
        >
          <a-tab-pane v-for="d in documents" :key="d.id">
            <template #tab>
              <span class="pdf-workspace__tab" @click="onManualTab">
                <a-tag v-if="d.role === 'tender'" class="pdf-workspace__tender">招标</a-tag>
                <DocBadge v-else :label="docLabel(documents, d.id)" />
              </span>
            </template>
          </a-tab-pane>
        </a-tabs>
      </div>
      <div v-else class="pdf-workspace__selects">
        <div class="pdf-workspace__select-col">
          <a-select
            v-model:value="leftDocId"
            size="small"
            class="pdf-workspace__select"
            :options="docOptions"
            @change="onManualTab"
          >
            <template #optionLabel="opt">
              <span class="pdf-workspace__option">
                <DocBadge :label="docLabel(documents, String(opt.value))" />
                <span class="pdf-workspace__option-name" :title="String(opt.label)">{{ opt.label }}</span>
              </span>
            </template>
            <template #option="{ value }">
              <span class="pdf-workspace__option">
                <DocBadge :label="docLabel(documents, value)" />
                <span class="pdf-workspace__option-name" :title="docName(value)">{{ docName(value) }}</span>
              </span>
            </template>
          </a-select>
        </div>
        <div class="pdf-workspace__select-col">
          <a-select
            v-model:value="rightDocId"
            size="small"
            class="pdf-workspace__select"
            :options="docOptions"
            @change="onManualTab"
          >
            <template #optionLabel="opt">
              <span class="pdf-workspace__option">
                <DocBadge :label="docLabel(documents, String(opt.value))" />
                <span class="pdf-workspace__option-name" :title="String(opt.label)">{{ opt.label }}</span>
              </span>
            </template>
            <template #option="{ value }">
              <span class="pdf-workspace__option">
                <DocBadge :label="docLabel(documents, value)" />
                <span class="pdf-workspace__option-name" :title="docName(value)">{{ docName(value) }}</span>
              </span>
            </template>
          </a-select>
        </div>
      </div>

      <a-tooltip :title="singlePane ? '展开双栏对比' : '收起为单栏'">
        <AppButton size="sm" variant="text" class="pdf-workspace__toggle" :class="{ 'pdf-workspace__toggle--center': !singlePane }" @click="singlePane = !singlePane">
          <ExpandOutlined v-if="singlePane" />
          <CompressOutlined v-else />
        </AppButton>
      </a-tooltip>
    </div>

    <div class="pdf-workspace__body" :class="{ 'pdf-workspace__body--single': singlePane }">
      <PdfViewer
        :file-url="docFileUrl(leftDocId)"
        :title="docName(leftDocId)"
        :page="leftPage"
        :total-pages="docPages(leftDocId)"
        :high="leftHigh"
        :scanning="scanningDocId === leftDocId"
        hide-original-label
        @update:page="leftPage = $event"
      />
      <PdfViewer
        v-if="!singlePane"
        :file-url="docFileUrl(rightDocId)"
        :title="docName(rightDocId)"
        :page="rightPage"
        :total-pages="docPages(rightDocId)"
        :high="rightHigh"
        :scanning="scanningDocId === rightDocId"
        hide-original-label
        @update:page="rightPage = $event"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AppButton } from '@shared/web'
import { CompressOutlined, ExpandOutlined } from '@ant-design/icons-vue'
import PdfViewer from './PdfViewer.vue'
import DocBadge from './DocBadge.vue'
import { docLabel } from '../constants'
import type { BlockRange, CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  pairActive?: { docAId: string, docBId: string } | null
  scanningDocId?: string | null
}>()

const emit = defineEmits<{
  tabManual: []
}>()

const singlePane = ref(false)
const leftDocId = ref('')
const rightDocId = ref('')
const leftPage = ref(1)
const rightPage = ref(1)
const leftHigh = ref<BlockRange[]>([])
const rightHigh = ref<BlockRange[]>([])

const docOptions = computed(() =>
  props.documents.map((d) => ({ value: d.id, label: d.fileName })),
)

watch(() => props.documents, (docs) => {
  if (!docs.length) return
  const bids = docs.filter((d) => d.role !== 'tender')
  if (!leftDocId.value || !docs.some((d) => d.id === leftDocId.value)) {
    leftDocId.value = bids[0]?.id ?? docs[0].id
  }
  if (!rightDocId.value || !docs.some((d) => d.id === rightDocId.value)) {
    rightDocId.value = bids[1]?.id ?? bids[0]?.id ?? docs[0].id
  }
}, { immediate: true })

watch(() => props.pairActive, (pair) => {
  if (!pair) return
  if (singlePane.value) singlePane.value = false
  leftDocId.value = pair.docAId
  rightDocId.value = pair.docBId
  leftPage.value = 1
  rightPage.value = 1
  leftHigh.value = []
  rightHigh.value = []
})

function docName(id: string): string {
  return props.documents.find((d) => d.id === id)?.fileName ?? ''
}

function docFileUrl(id: string): string {
  return props.documents.find((d) => d.id === id)?.fileUrl ?? ''
}

function docPages(id: string): number {
  return props.documents.find((d) => d.id === id)?.pages ?? 0
}

function onManualTab(): void {
  emit('tabManual')
}

/** 证据溯源：单份文档单栏定位，两份及以上自动展开双栏并分别定位高亮；无可用 refs 时不画误导性整页高亮。 */
function locate(ev: EvidenceItem): void {
  const [a, b] = ev.docIds
  if (b && singlePane.value) singlePane.value = false
  if (a) {
    leftDocId.value = a
    const refs = refsOf(ev, a)
    leftPage.value = refs[0]?.page ?? 1
    leftHigh.value = refs
  }
  if (b) {
    rightDocId.value = b
    const refs = refsOf(ev, b)
    rightPage.value = refs[0]?.page ?? 1
    rightHigh.value = refs
  }
}

function locateRefs(refs: BlockRange[]): void {
  if (!refs.length) return
  const [a, b] = refs
  if (b && singlePane.value) singlePane.value = false
  if (a) {
    leftDocId.value = a.docId
    leftPage.value = a.page
    leftHigh.value = [a]
  }
  if (b) {
    rightDocId.value = b.docId
    rightPage.value = b.page
    rightHigh.value = [b]
  }
}

function refsOf(ev: EvidenceItem, docId: string): BlockRange[] {
  return ev.refs.filter((r) => r.docId === docId)
}

/* function fullPageRefs(ev: EvidenceItem, docId: string): BlockRange[] {
  return [{ docId, page: 1, bbox: [0, 0, 1, 1], pairId: ev.id }]
}

*/
/** 单文档定位（文档列表/来源 chip 跳转用）。 */
function locateDoc(docId: string, page = 1): void {
  leftDocId.value = docId
  leftPage.value = page
  leftHigh.value = []
}

/** 解析完成自动切换 Tab（由 index 在 10 秒手动保护后调用）。 */
function focusDoc(docId: string): void {
  if (!props.documents.some((d) => d.id === docId)) return
  leftDocId.value = docId
  leftPage.value = 1
  leftHigh.value = []
}

defineExpose({ locate, locateRefs, locateDoc, focusDoc })
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.pdf-workspace {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.pdf-workspace__bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-sm;
  flex-shrink: 0;
}

  .pdf-workspace__bar {
    position: relative;
  }

  .pdf-workspace__tabs-single {
    flex: 1;
    min-width: 0;
  }

  .pdf-workspace__selects {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: @spacing-md;
    flex: 1;
    min-width: 0;
  }

  .pdf-workspace__select-col {
    display: flex;
    flex-direction: column;
    align-items: stretch;
    gap: 2px;
    min-width: 0;
  }

.pdf-workspace__tabs {
  flex: 1;
  min-width: 0;

  :deep(.ant-tabs-nav) { margin-bottom: 0; }
  :deep(.ant-tabs-tab) { padding: 4px 8px; }
}

.pdf-workspace__tab {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: @font-size-xs;
}

.pdf-workspace__tender {
  margin-inline-end: 0;
  font-size: @font-size-xs;
  line-height: 18px;
}

.pdf-workspace__select {
  flex: 1;
    min-width: 0;

  flex-shrink: 0;
}

.pdf-workspace__option {
  display: inline-flex;
  align-items: center;
  gap: @spacing-sm;
  min-width: 0;
  max-width: 100%;
}

.pdf-workspace__option-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pdf-workspace__toggle {
  flex-shrink: 0;
}

  .pdf-workspace__toggle--center {
    position: absolute;
    left: 50%;
    top: 50%;
    transform: translate(-50%, -50%);
    z-index: 1;
  }

.pdf-workspace__body {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: @spacing-md;

  &--single {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
