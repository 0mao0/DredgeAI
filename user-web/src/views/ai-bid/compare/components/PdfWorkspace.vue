<template>
  <div class="pdf-workspace">
    <div class="pdf-workspace__bar">
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
              <template v-else>{{ docLabel(documents, d.id) }}</template>
              <span
                class="pdf-workspace__dot"
                :class="`pdf-workspace__dot--${d.parseStatus}`"
                :title="statusTitle(d)"
              />
            </span>
          </template>
        </a-tab-pane>
      </a-tabs>

      <a-select
        v-if="!collapsed"
        v-model:value="rightDocId"
        size="small"
        class="pdf-workspace__select"
        :options="docOptions"
      />

      <a-tooltip :title="collapsed ? '展开双栏对比' : '收起为单栏'">
        <a-button size="small" type="text" class="pdf-workspace__toggle" @click="emit('update:collapsed', !collapsed)">
          <ExpandOutlined v-if="collapsed" />
          <CompressOutlined v-else />
        </a-button>
      </a-tooltip>
    </div>

    <div class="pdf-workspace__body" :class="{ 'pdf-workspace__body--single': collapsed }">
      <PdfViewer
        :file-url="docFileUrl(leftDocId)"
        :title="docName(leftDocId)"
        :page="leftPage"
        :total-pages="docPages(leftDocId)"
        :high="leftHigh"
        :scanning="scanningDocId === leftDocId"
        @update:page="leftPage = $event"
      />
      <PdfViewer
        v-if="!collapsed"
        :file-url="docFileUrl(rightDocId)"
        :title="docName(rightDocId)"
        :page="rightPage"
        :total-pages="docPages(rightDocId)"
        :high="rightHigh"
        :scanning="scanningDocId === rightDocId"
        @update:page="rightPage = $event"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CompressOutlined, ExpandOutlined } from '@ant-design/icons-vue'
import PdfViewer from './PdfViewer.vue'
import { docLabel } from '../constants'
import type { BlockRange, CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  collapsed: boolean
  pairActive?: { docAId: string, docBId: string } | null
  scanningDocId?: string | null
}>()

const emit = defineEmits<{
  'update:collapsed': [value: boolean]
  'tabManual': []
}>()

const leftDocId = ref('')
const rightDocId = ref('')
const leftPage = ref(1)
const rightPage = ref(1)
const leftHigh = ref<BlockRange[]>([])
const rightHigh = ref<BlockRange[]>([])

const docOptions = computed(() =>
  props.documents.map((d) => ({ value: d.id, label: `${docLabel(props.documents, d.id)} · ${d.fileName}` })),
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
  if (props.collapsed) emit('update:collapsed', false)
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

function statusTitle(d: CompareDocMeta): string {
  return {
    pending: '等待解析',
    parsing: '解析中',
    done: '解析完成',
    failed: d.failReason ?? '解析失败',
  }[d.parseStatus] ?? d.parseStatus
}

function onManualTab(): void {
  emit('tabManual')
}

/** 证据溯源：单份文档单栏定位，两份及以上自动展开双栏并分别定位高亮。 */
function locate(ev: EvidenceItem): void {
  const refs = ev.refs.length
    ? ev.refs
    : ev.docIds.map((docId) => ({ docId, page: 1, bbox: [0, 0, 1, 1] as [number, number, number, number] }))
  const [a, b] = ev.docIds
  if (b && props.collapsed) emit('update:collapsed', false)
  if (a) {
    leftDocId.value = a
    leftPage.value = refs.find((r) => r.docId === a)?.page ?? 1
    leftHigh.value = refs.filter((r) => r.docId === a)
  }
  if (b) {
    rightDocId.value = b
    rightPage.value = refs.find((r) => r.docId === b)?.page ?? 1
    rightHigh.value = refs.filter((r) => r.docId === b)
  }
}

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

defineExpose({ locate, locateDoc, focusDoc })
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

.pdf-workspace__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: @text-tertiary;

  &--parsing { background: @brand-primary; animation: pdf-workspace-pulse 1.2s ease-in-out infinite; }
  &--done { background: @success; }
  &--failed { background: @danger; }
}

.pdf-workspace__select {
  width: 200px;
  flex-shrink: 0;
}

.pdf-workspace__toggle {
  flex-shrink: 0;
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

@keyframes pdf-workspace-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}

@media (prefers-reduced-motion: reduce) {
  .pdf-workspace__dot--parsing {
    animation: none;
  }
}
</style>
