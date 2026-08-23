<template>
  <div class="standard-page">
    <PageHeader title="规范问答">
      <template #extra>
        <a-segmented v-model:value="viewMode" :options="viewModeOptions" />
      </template>
    </PageHeader>

    <!-- 树模式：树 | PDF 阅读 | AI 问答 -->
    <div v-if="viewMode === 'tree'" class="standard-tree-mode">
      <aside class="standard-tree-mode__left">
        <SectionCard title="标准库" flush>
          <a-input-search
            v-model:value="treeSearch"
            placeholder="搜索标准名称 / 编号"
            allow-clear
            class="tree-search"
          />
          <a-tree
            :tree-data="treeData"
            :field-names="{ title: 'name', key: 'id' }"
            :default-expand-all="true"
            :selected-keys="currentId ? [currentId] : []"
            class="standard-tree"
            @select="(keys: (string | number)[]) => keys[0] && selectStandard(String(keys[0]))"
          />
        </SectionCard>
      </aside>

      <main class="standard-tree-mode__center">
        <StandardPdfViewer :file-url="pdfUrl" empty-title="请先在左侧选择标准" />
      </main>

      <aside class="standard-tree-mode__right">
        <StandardChat :standard-id="currentId" :standard-name="currentName" />
      </aside>
    </div>

    <!-- 列表模式：同 admin-web 完整表格，仅只读（无删除/编辑等管理操作） -->
    <div v-else class="standard-list-mode">
      <DataTable
        v-model:query="query"
        class="standard-list-table"
        :columns="columns"
        :data-source="tableRecords"
        :pagination="{ current: page, pageSize, total }"
        :loading="loading"
        :filters="filters"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.key === 'index'">
            {{ (page - 1) * pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'level'">
            <a-tag v-if="record.level">{{ record.level }}</a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">{{ record.status || '—' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'source'">
            <a-tag :color="record.source === 'manual' ? 'orange' : 'blue'">
              {{ record.source === 'manual' ? '人工补录' : '同步' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <AppButton variant="link" size="sm" @click="openViewer(record)">查看</AppButton>
          </template>
        </template>
      </DataTable>
    </div>

    <!-- 抽屉：PDF 预览（复用 StandardPdfViewer，与 admin-web 查看一致，不跳树模式） -->
    <a-drawer
      v-model:open="viewerVisible"
      :title="viewerTarget?.name || '标准原文'"
      width="760px"
      :body-style="{ padding: 0 }"
      :root-class-name="detailVisible ? 'viewer-drawer--pushed' : undefined"
      destroy-on-close
    >
      <template #extra>
        <a-tooltip title="标准详情">
          <AppButton size="sm" variant="text" @click="detailVisible = true">
            <InfoCircleOutlined />
          </AppButton>
        </a-tooltip>
      </template>
      <div class="viewer-drawer">
        <StandardPdfViewer
          :file-url="viewerTarget ? `/mock/standards/${viewerTarget.id}.pdf` : null"
          :parsed-content="viewerContent"
          empty-title="请先在左侧选择标准"
        />
      </div>
    </a-drawer>

    <StandardDetailDrawer
      v-model:open="detailVisible"
      :standard="viewerStandard"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { AppButton, DataTable, StandardDetailDrawer, StandardPdfViewer } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { InfoCircleOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import StandardChat from './components/StandardChat.vue'
import { getStandardDocument, getStandardRecords } from '@/api/modules/standard'
import type { StandardProperty, StandardRecord } from '@/types'

type ViewMode = 'list' | 'tree'

const viewMode = ref<ViewMode>('tree')
const viewModeOptions = [
  { label: '列表', value: 'list' },
  { label: '树', value: 'tree' },
]

/* —— 树模式数据：一次加载全量（mock 规模小；真实后端按需分页/分类树） —— */
const treeRecords = ref<StandardRecord[]>([])
const treeSearch = ref('')
const treeData = computed(() => {
  const q = treeSearch.value.trim().toLowerCase()
  const list = q
    ? treeRecords.value.filter((r) => r.name.toLowerCase().includes(q) || r.code?.toLowerCase().includes(q))
    : treeRecords.value
  return list.map((r) => ({ id: r.id, name: r.name, code: r.code ?? '' }))
})

/* —— 列表模式数据：服务端分页 + 筛选 —— */
const pageSize = 15
const page = ref(1)
const total = ref(0)
const tableRecords = ref<StandardRecord[]>([])
const loading = ref(false)
const query = ref<{ keyword?: string, industry?: string, nature?: string, level?: string, status?: string, year?: number }>({})

const columns: DataTableColumn[] = [
  { title: '序号', key: 'index', width: 70, minWidth: 60, resizable: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: 220, minWidth: 160, resizable: true },
  { title: '编号', dataIndex: 'code', key: 'code', width: 180, minWidth: 140, resizable: true },
  { title: '行业', dataIndex: 'industry', key: 'industry', width: 90, minWidth: 80, resizable: true },
  { title: '性质', dataIndex: 'nature', key: 'nature', width: 90, minWidth: 80, resizable: true },
  { title: '级别', key: 'level', width: 110, minWidth: 90, resizable: true },
  { title: '状态', key: 'status', width: 90, minWidth: 80, resizable: true },
  { title: '年份', dataIndex: 'year', key: 'year', width: 80, minWidth: 70, resizable: true },
  { title: '来源', key: 'source', width: 90, minWidth: 80, resizable: true },
  { title: '同步时间', dataIndex: 'syncedAt', key: 'syncedAt', width: 170, minWidth: 140, resizable: true },
  { title: '操作', key: 'action', width: 80, minWidth: 80, fixed: 'right', resizable: true },
]

/* —— 选中标准（两种模式共享） —— */
const currentId = ref<string | null>(null)
const currentName = computed(
  () => treeRecords.value.find((r) => r.id === currentId.value)?.name ?? '',
)

/** mock 静态 PDF 地址（真实后端就绪后换 getStandardRecordFileUrl 流式接口） */
const pdfUrl = computed(() => (currentId.value ? `/mock/standards/${currentId.value}.pdf` : null))

function selectStandard(id: string): void {
  currentId.value = id
}

/* —— 列表模式「查看」：抽屉弹 PDF 阅读器（复用 StandardPdfViewer） —— */
const viewerVisible = ref(false)
const viewerTarget = ref<StandardRecord | null>(null)
const viewerStandard = ref<StandardProperty | null>(null)
const viewerContent = ref('')
const detailVisible = ref(false)

async function openViewer(record: StandardRecord): Promise<void> {
  viewerTarget.value = record
  viewerVisible.value = true
  viewerStandard.value = {
    id: record.id,
    name: record.name,
    code: record.code ?? '',
    industry: record.industry ?? undefined,
    nature: record.nature ?? undefined,
    level: record.level ?? undefined,
    status: record.status ?? undefined,
    issuer: record.department ?? undefined,
    publishYear: record.year ?? undefined,
    description: record.content ?? undefined,
  }
  viewerContent.value = ''
  try {
    const doc = await getStandardDocument(record.id)
    viewerContent.value = doc?.content ?? ''
  } catch {
    viewerContent.value = ''
  }
}

/* —— 列表模式查询 —— */
async function fetchRecords(): Promise<void> {
  loading.value = true
  try {
    const res = await getStandardRecords({
      keyword: query.value.keyword,
      industry: query.value.industry,
      nature: query.value.nature,
      level: query.value.level,
      status: query.value.status,
      year: query.value.year,
      skipCount: (page.value - 1) * pageSize,
      maxResultCount: pageSize,
    })
    tableRecords.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

function handleTableChange(pagination: unknown): void {
  page.value = (pagination as { current?: number } | null)?.current ?? 1
  fetchRecords()
}

watch(query, () => {
  page.value = 1
  fetchRecords()
}, { deep: true })

function statusColor(status?: string | null): string {
  if (status === '现行') return 'green'
  if (status === '作废' || status === '废止') return 'red'
  if (status === '即将实施') return 'blue'
  return 'default'
}

const industryOptions = [
  { value: '水利', label: '水利' },
  { value: '建筑', label: '建筑' },
  { value: '交通', label: '交通' },
  { value: '环保', label: '环保' },
  { value: '综合', label: '综合' },
]
const natureOptions = [
  { value: '强制', label: '强制' },
  { value: '推荐', label: '推荐' },
  { value: '指导', label: '指导' },
]
const levelOptions = [
  { value: '国家标准', label: '国家标准' },
  { value: '行业标准', label: '行业标准' },
  { value: '地方标准', label: '地方标准' },
  { value: '团体标准', label: '团体标准' },
  { value: '企业标准', label: '企业标准' },
  { value: '国际标准', label: '国际标准' },
  { value: '法律法规', label: '法律法规' },
]
const statusOptions = [
  { value: '现行', label: '现行' },
  { value: '即将实施', label: '即将实施' },
  { value: '作废', label: '作废' },
]
const yearOptions = [
  { value: 2022, label: '2022' },
  { value: 2021, label: '2021' },
  { value: 2020, label: '2020' },
  { value: 2019, label: '2019' },
  { value: 2018, label: '2018' },
]

const filters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索名称 / 编号', width: 220 },
  { key: 'industry', type: 'select', placeholder: '行业', width: 120, options: industryOptions },
  { key: 'nature', type: 'select', placeholder: '性质', width: 100, options: natureOptions },
  { key: 'level', type: 'select', placeholder: '级别', width: 130, options: levelOptions },
  { key: 'status', type: 'select', placeholder: '状态', width: 110, options: statusOptions },
  { key: 'year', type: 'select', placeholder: '年份', width: 100, options: yearOptions },
]

onMounted(async () => {
  const treeRes = await getStandardRecords({ maxResultCount: 500 })
  treeRecords.value = treeRes.items
  if (treeRes.items.length > 0) {
    currentId.value = treeRes.items[0].id
  }
  await fetchRecords()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.standard-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: @page-padding;
  box-sizing: border-box;
}

.standard-tree-mode {
  flex: 1;
  min-height: 0;
  display: flex;
  gap: @spacing-md;

  &__left {
    width: 240px;
    flex-shrink: 0;
    height: 100%;
    :deep(.section-card) {
      height: 100%;
      display: flex;
      flex-direction: column;
      .section-card-body {
        flex: 1;
        min-height: 0;
        overflow-y: auto;
      }
    }
  }
  &__center {
    flex: 1;
    min-width: 0;
    height: 100%;
  }
  &__right {
    width: 340px;
    flex-shrink: 0;
    height: 100%;
  }
}

.tree-search { margin-bottom: @spacing-md; }

.standard-tree {
  :deep(.ant-tree-node-content-wrapper) { padding-right: 8px; }
}

/* —— 列表模式 —— */
.standard-list-mode {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: @spacing-base;
}

.standard-list-table {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  :deep(.data-table-card) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    :deep(.section-card-body) {
      flex: 1;
      min-height: 0;
      display: flex;
      flex-direction: column;
    }
    :deep(.ant-table-wrapper) {
      flex: 1;
      min-height: 0;
    }
  }
}

.viewer-drawer {
  height: 100%;
}
</style>

<style lang="less">
/* 详情抽屉弹出时，查看抽屉整体左移，形成“被推开”的效果（抽屉渲染在 body portal，不能用 scoped） */
.viewer-drawer--pushed .ant-drawer-content-wrapper {
  transform: translateX(-440px) !important;
}
</style>
