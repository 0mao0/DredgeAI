<template>
  <div class="pdf-combo">
    <div class="pdf-combo__bar">
      <a-select
        v-model:value="leftDocId"
        size="small"
        class="pdf-combo__select"
        :options="docOptions"
      />
      <a-select
        v-if="!collapsed"
        v-model:value="rightDocId"
        size="small"
        class="pdf-combo__select"
        :options="docOptions"
      />
      <a-tooltip :title="collapsed ? '展开双栏对比' : '收起为单栏'">
        <a-button size="small" type="text" class="pdf-combo__toggle" @click="emit('update:collapsed', !collapsed)">
          <ExpandOutlined v-if="collapsed" />
          <CompressOutlined v-else />
        </a-button>
      </a-tooltip>
    </div>

    <div class="pdf-combo__body" :class="{ 'pdf-combo__body--single': collapsed }">
      <PdfViewer
        :src="leftDocId"
        :title="docName(leftDocId)"
        :page="leftPage"
        :total-pages="docPages(leftDocId)"
        :high="leftHigh"
      />
      <PdfViewer
        v-if="!collapsed"
        :src="rightDocId"
        :title="docName(rightDocId)"
        :page="rightPage"
        :total-pages="docPages(rightDocId)"
        :high="rightHigh"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { CompressOutlined, ExpandOutlined } from '@ant-design/icons-vue'
import PdfViewer from './PdfViewer.vue'
import type { BlockRange, CompareDocMeta, EvidenceItem } from '@/types'

const props = defineProps<{
  documents: CompareDocMeta[]
  collapsed: boolean
}>()

const emit = defineEmits<{ 'update:collapsed': [value: boolean] }>()

const leftDocId = ref('')
const rightDocId = ref('')
const leftPage = ref(1)
const rightPage = ref(1)
const leftHigh = ref<BlockRange[]>([])
const rightHigh = ref<BlockRange[]>([])

watch(() => props.documents, (docs) => {
  if (!docs.length) return
  if (!leftDocId.value || !docs.some((d) => d.id === leftDocId.value)) {
    leftDocId.value = docs[0].id
  }
  if (!rightDocId.value || !docs.some((d) => d.id === rightDocId.value)) {
    rightDocId.value = docs[1]?.id ?? docs[0].id
  }
  leftPage.value = 1
  rightPage.value = 1
  leftHigh.value = []
  rightHigh.value = []
}, { immediate: true })

const docOptions = computed(() =>
  props.documents.map((d, i) => ({ value: d.id, label: `${String.fromCharCode(65 + i)} · ${d.fileName}` })),
)

function docName(id: string): string {
  return props.documents.find((d) => d.id === id)?.fileName ?? ''
}

function docPages(id: string): number {
  return props.documents.find((d) => d.id === id)?.pages ?? 0
}

/** 证据溯源：双栏定位到证据涉及的两份文档对应页并高亮 */
function locate(ev: EvidenceItem): void {
  const [a, b] = ev.docIds
  if (b && props.collapsed) emit('update:collapsed', false)
  leftDocId.value = a
  if (b) rightDocId.value = b
  leftPage.value = ev.refs.find((r) => r.docId === a)?.page ?? 1
  if (b) rightPage.value = ev.refs.find((r) => r.docId === b)?.page ?? 1
  leftHigh.value = ev.refs.filter((r) => r.docId === a)
  rightHigh.value = b ? ev.refs.filter((r) => r.docId === b) : []
}

/** 单文档定位（文档列表跳转用） */
function locateDoc(docId: string, page = 1): void {
  leftDocId.value = docId
  leftPage.value = page
  leftHigh.value = []
}

defineExpose({ locate, locateDoc })
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.pdf-combo {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.pdf-combo__bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-sm;
  flex-shrink: 0;
}

.pdf-combo__select {
  flex: 1;
  min-width: 0;
}

.pdf-combo__toggle {
  flex-shrink: 0;
}

.pdf-combo__body {
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
