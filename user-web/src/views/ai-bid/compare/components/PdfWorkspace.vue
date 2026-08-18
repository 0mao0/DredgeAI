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
      <AppButton
        v-if="lastLocateTask"
        size="sm"
        variant="text"
        class="pdf-workspace__mode-toggle"
        @click="toggleViewMode"
      >
        {{ viewMode === 'pages' ? '查看整篇' : '证据页' }}
      </AppButton>
    </div>

    <div class="pdf-workspace__body" :class="{ 'pdf-workspace__body--single': singlePane }">
      <template v-if="viewMode === 'pages'">
        <PdfViewer
          :file-url="docFileUrl(leftDocId)"
          :title="docName(leftDocId)"
          :page="leftPage"
          :high="leftHigh"
          :page-range="leftRange"
          :scanning="scanningDocId === leftDocId"
          hide-original-label
          @update:page="leftPage = $event"
          @loaded="(url) => onViewerLoaded(leftDocId, url)"
        />
        <PdfViewer
          v-if="!singlePane"
          :file-url="docFileUrl(rightDocId)"
          :title="docName(rightDocId)"
          :page="rightPage"
          :high="rightHigh"
          :page-range="rightRange"
          :scanning="scanningDocId === rightDocId"
          hide-original-label
          @update:page="rightPage = $event"
          @loaded="(url) => onViewerLoaded(rightDocId, url)"
        />
      </template>
      <template v-else>
        <PdfViewer
          :file-url="docFileUrl(leftDocId)"
          :title="docName(leftDocId)"
          :page="leftPage"
          :high="leftHigh"
          :scanning="scanningDocId === leftDocId"
          hide-original-label
          @update:page="leftPage = $event"
          @loaded="(url) => onViewerLoaded(leftDocId, url)"
        />
        <PdfViewer
          v-if="!singlePane"
          :file-url="docFileUrl(rightDocId)"
          :title="docName(rightDocId)"
          :page="rightPage"
          :high="rightHigh"
          :scanning="scanningDocId === rightDocId"
          hide-original-label
          @update:page="rightPage = $event"
          @loaded="(url) => onViewerLoaded(rightDocId, url)"
        />
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import type { Ref } from 'vue'
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
const viewMode = ref<'pages' | 'full'>('full')
const lastLocateTask = ref<LocateTask | null>(null)

/** 证据页视图只渲染 refs 覆盖的页范围；跨度超过该上限时回退整篇视图 */
const MAX_PAGES_MODE = 24

/** 最近一次定位请求；PDF 未加载完成时暂存，加载完成后重放，保证首次点击也能跳页高亮 */
let pendingLocate: LocateTask | null = null
const loadedDocIds = ref(new Set<string>())

const leftHigh = computed<BlockRange[]>(() => refsForDoc(lastLocateTask.value, leftDocId.value))
const rightHigh = computed<BlockRange[]>(() => refsForDoc(lastLocateTask.value, rightDocId.value))
const leftRange = computed<number[]>(() => pageRange(leftHigh.value))
const rightRange = computed<number[]>(() => pageRange(rightHigh.value))

const docOptions = computed(() =>
  props.documents.map((d) => ({ value: d.id, label: d.fileName })),
)

watch(() => props.documents, (docs) => {
  if (!docs.length) return
  // 切换任务后清空旧定位，避免上一任务 refs 错配到新文档
  pendingLocate = null
  lastLocateTask.value = null
  viewMode.value = 'full'
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
  pendingLocate = null
  lastLocateTask.value = null
  viewMode.value = 'full'
  leftDocId.value = pair.docAId
  rightDocId.value = pair.docBId
  leftPage.value = 1
  rightPage.value = 1
})

function docName(id: string): string {
  return props.documents.find((d) => d.id === id)?.fileName ?? ''
}

function docFileUrl(id: string): string {
  return props.documents.find((d) => d.id === id)?.fileUrl ?? ''
}

function onManualTab(): void {
  emit('tabManual')
}

/** 定位任务：证据整体定位或 refs 多块定位，二者择一 */
type LocateTask
  = | { kind: 'evidence', ev: EvidenceItem }
    | { kind: 'refs', refs: BlockRange[] }

/** 证据溯源：单份文档单栏定位，两份及以上自动展开双栏并分别定位高亮；无可用 refs 时不画误导性整页高亮。 */
function locate(ev: EvidenceItem): void {
  pendingLocate = { kind: 'evidence', ev }
  void applyLocate(pendingLocate, 'auto')
}

/** 多文档定位：按 docId 分组，左栏展示第一组全部 refs、右栏展示第二组全部 refs
 *  （片段定位传入的是 [A 多块..., B 多块...]，不能简单取前两个——那会是同一文档的两块）。 */
function locateRefs(refs: BlockRange[]): void {
  if (!refs.length) return
  pendingLocate = { kind: 'refs', refs }
  void applyLocate(pendingLocate, 'auto')
}

type LocateMode = 'auto' | 'pages' | 'full'

async function applyLocate(task: LocateTask, mode: LocateMode = 'auto', force = false): Promise<void> {
  lastLocateTask.value = task
  if (task.kind === 'evidence') {
    const ev = task.ev
    const [a, b] = ev.docIds
    if (b && singlePane.value) singlePane.value = false
    if (a) leftDocId.value = a
    if (b) rightDocId.value = b
  } else {
    const refs = task.refs
    const byDoc = new Map<string, BlockRange[]>()
    for (const r of refs) {
      const list = byDoc.get(r.docId) ?? []
      list.push(r)
      byDoc.set(r.docId, list)
    }
    const groups = [...byDoc.values()]
    const [aRefs, bRefs] = [groups[0] ?? [], groups[1] ?? []]
    if (aRefs.length && bRefs.length && singlePane.value) singlePane.value = false
    if (aRefs.length) leftDocId.value = aRefs[0].docId
    if (bRefs.length) rightDocId.value = bRefs[0].docId
  }

  leftPage.value = leftRange.value[0] ?? 1
  rightPage.value = rightRange.value[0] ?? 1

  if (mode === 'full') {
    viewMode.value = 'full'
  } else if (mode === 'pages') {
    viewMode.value = 'pages'
  } else {
    // 证据页视图：只渲染 refs 覆盖的页范围（跨页片段完整呈现）；跨度异常大时回退整篇
    const spans = Math.max(leftRange.value.length, rightRange.value.length)
    viewMode.value = spans > 0 && spans <= MAX_PAGES_MODE ? 'pages' : 'full'
  }
  // 强制重放：文档加载完成后页号可能已是目标页，docs-ui 的 currentPdfPage 监听
  // 只在值变化时触发，先把页号归零再设回，确保重新执行 scrollToPdfPage。
  if (force) {
    await forceRefire()
  }
}

async function forceRefire(): Promise<void> {
  await nextTick()
  const targets: Array<[Ref<number>, number]> = [
    [leftPage, leftPage.value],
    [rightPage, rightPage.value],
  ]
  for (const [pageRef, target] of targets) {
    if (target <= 0) continue
    pageRef.value = 0
    await nextTick()
    pageRef.value = target
  }
}

function locateDocIds(task: LocateTask): string[] {
  if (task.kind === 'evidence') return task.ev.docIds.filter((id): id is string => !!id)
  return [...new Set(task.refs.map((r) => r.docId))]
}

/** PDF 文档加载完成：若仍有未重放的定位请求且涉及文档均已就绪，稍后强制重放跳页 */
function onViewerLoaded(docId: string, url: string): void {
  const doc = props.documents.find((d) => d.id === docId)
  if (!doc || doc.fileUrl !== url) return
  loadedDocIds.value = new Set([...loadedDocIds.value, docId])
  const task = pendingLocate
  if (!task) return
  const ids = locateDocIds(task)
  if (!ids.length || !ids.every((id) => loadedDocIds.value.has(id))) return
  window.setTimeout(() => {
    if (pendingLocate !== task) return
    pendingLocate = null
    void applyLocate(task, viewMode.value, true)
  }, 300)
}

/** 取定位任务中指定文档的 refs（证据按 docIds 过滤，refs 任务按 docId 过滤） */
function refsForDoc(task: LocateTask | null, docId: string): BlockRange[] {
  if (!task) return []
  if (task.kind === 'evidence') return task.ev.refs.filter((r) => r.docId === docId)
  return task.refs.filter((r) => r.docId === docId)
}

function pageRange(refs: BlockRange[]): number[] {
  return [...new Set(refs.map((r) => r.page))].sort((a, b) => a - b)
}

/** 证据页视图 / 整篇视图切换：重新按最近一次定位应用对应模式 */
function toggleViewMode(): void {
  const task = lastLocateTask.value
  if (!task) return
  void applyLocate(task, viewMode.value === 'pages' ? 'full' : 'pages')
}

/* function fullPageRefs(ev: EvidenceItem, docId: string): BlockRange[] {
  return [{ docId, page: 1, bbox: [0, 0, 1, 1], pairId: ev.id }]
}

*/
/** 单文档定位（文档列表/来源 chip 跳转用）。 */
function locateDoc(docId: string, page = 1): void {
  pendingLocate = null
  lastLocateTask.value = null
  viewMode.value = 'full'
  leftDocId.value = docId
  leftPage.value = page
}

/** 解析完成自动切换 Tab（由 index 在 10 秒手动保护后调用）。 */
function focusDoc(docId: string): void {
  if (!props.documents.some((d) => d.id === docId)) return
  pendingLocate = null
  lastLocateTask.value = null
  viewMode.value = 'full'
  leftDocId.value = docId
  leftPage.value = 1
}

defineExpose({ locate, locateRefs, locateDoc, focusDoc, toggleViewMode })
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
