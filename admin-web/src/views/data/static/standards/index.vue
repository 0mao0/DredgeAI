<template>
  <div class="page-container">
    <PageHeader title="标准规范" description="知识库标准规范的查询、解析与维护">
      <template #extra>
        <a-space :size="8">
          <AppButton v-if="uploadHasTasks" size="sm" @click="uploadDrawerVisible = true">
            <CloudUploadOutlined />
            上传任务
            <a-badge :count="uploadRunningCount" :offset="[4, -2]" />
          </AppButton>
          <AppButton size="sm" class="standards-header-refresh" :loading="refreshing" @click="handleRefresh">
            <ReloadOutlined />
            刷新
          </AppButton>
          <AppButton variant="primary" @click="uploadModalVisible = true">
            <UploadOutlined />
            上传文档
          </AppButton>
        </a-space>
      </template>
    </PageHeader>

    <a-result v-if="error" status="error" title="加载失败" :sub-title="error">
      <template #extra>
        <AppButton variant="primary" @click="fetchStandards">重试</AppButton>
      </template>
    </a-result>

    <div v-else ref="tableContainerRef" class="standards-table-wrap">
      <div class="standards-filter-bar">
        <a-input
          v-model:value="query.keyword"
          placeholder="搜索名称 / 编号"
          allow-clear
          style="width: 220px"
        />
        <a-select v-model:value="query.industry" allow-clear placeholder="行业" style="width: 120px">
          <a-select-option v-for="opt in industryOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.nature" allow-clear placeholder="性质" style="width: 110px">
          <a-select-option v-for="opt in natureOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.level" allow-clear placeholder="级别" style="width: 130px">
          <a-select-option v-for="opt in levelOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.status" allow-clear placeholder="状态" style="width: 110px">
          <a-select-option v-for="opt in statusOptions" :key="opt" :value="opt">{{ opt }}</a-select-option>
        </a-select>
        <a-select v-model:value="query.publishYear" allow-clear placeholder="发布年份" style="width: 120px">
          <a-select-option v-for="year in yearOptions" :key="year" :value="year">{{ year }}</a-select-option>
        </a-select>
        <AppButton size="sm" class="standards-filter-bar__reset" @click="handleReset">重置</AppButton>
        <AppButton size="sm" class="standards-filter-bar__refresh" style="display:none" :loading="refreshing" @click="handleRefresh">
          <ReloadOutlined />
          刷新
        </AppButton>
      </div>

      <SectionCard nopad class="standards-table-card">
        <div v-if="selectedRowKeys.length" class="standards-batch-bar">
          <span class="standards-batch-bar__count">已选 {{ selectedRowKeys.length }} 条</span>
          <a-space :size="8">
            <AppButton size="sm" @click="openBatchParse">批量解析</AppButton>
            <a-popconfirm
              title="确定删除选中的标准？"
              description="删除后不可恢复"
              @confirm="handleBatchDelete"
            >
              <AppButton size="sm" danger>批量删除</AppButton>
            </a-popconfirm>
          </a-space>
        </div>
        <a-table
          class="standards-table"
          :data-source="standards"
          :columns="columns"
          :row-selection="rowSelection"
          :pagination="{
            current: page,
            pageSize,
            total,
            showSizeChanger: false,
            showTotal: (t: number) => `共 ${t} 条`,
          }"
          :loading="loading"
          :scroll="{ x: scrollX }"
          row-key="id"
          size="small"
          :locale="{ emptyText: '暂无数据' }"
          @resize-column="handleResizeColumn"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'index'">
              {{ (page - 1) * pageSize + index + 1 }}
            </template>
            <template v-else-if="column.key === 'status'">
              <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
            </template>
            <template v-else-if="column.key === 'action'">
              <div class="action-cell">
                <AppButton variant="link" size="sm" @click="openViewer(record)">查看</AppButton>
                <AppButton variant="link" size="sm" @click="openParse(record)">解析</AppButton>
                <a-popconfirm title="确认删除该标准？" placement="left" @confirm="handleDelete(record)">
                  <AppButton variant="link" size="sm" danger>删除</AppButton>
                </a-popconfirm>
              </div>
            </template>
          </template>
        </a-table>
      </SectionCard>
    </div>

    <a-drawer
      v-model:open="viewerVisible"
      :title="viewerTarget?.name || '标准原文'"
      width="960px"
      :body-style="{ padding: 0 }"
      @close="resetViewer"
    >
      <template #extra>
        <AppButton size="sm" @click="openEdit(viewerTarget)">
          <InfoCircleOutlined />
          详情 / 编辑
        </AppButton>
      </template>
      <div class="viewer-body">
        <StandardPdfViewer
          v-if="viewerTarget"
          :file-url="getStandardFileUrl(viewerTarget.id)"
          :page="viewerPage"
          :highlights="viewerTarget?.highlights ?? []"
          :standard="viewerTarget"
          @update:page="viewerPage = $event"
        />
      </div>
    </a-drawer>

    <a-modal
      v-model:open="parseVisible"
      :title="`AI 解析 - ${parseTarget?.name || ''}`"
      width="640px"
      :footer="null"
      @cancel="resetParse"
    >
      <a-skeleton v-if="parseLoading" :paragraph="{ rows: 6 }" />
      <a-result v-else-if="parseError" status="error" :title="parseError" />
      <div v-else-if="parseResult" class="parse-result">
        <p class="parse-result__summary">{{ parseResult.summary }}</p>
        <div class="parse-result__block">
          <h4 class="parse-result__title">关键要点</h4>
          <ul v-if="parseResult.keyPoints.length" class="parse-result__list">
            <li v-for="point in parseResult.keyPoints" :key="point">{{ point }}</li>
          </ul>
          <a-empty v-else image="simple" description="暂无要点" />
        </div>
        <div class="parse-result__block">
          <h4 class="parse-result__title">风险提示</h4>
          <ul v-if="parseResult.riskWarnings.length" class="parse-result__list parse-result__list--risk">
            <li v-for="warning in parseResult.riskWarnings" :key="warning">{{ warning }}</li>
          </ul>
          <a-empty v-else image="simple" description="暂无风险提示" />
        </div>
      </div>
    </a-modal>

    <a-modal
      v-model:open="editVisible"
      :title="`编辑标准 - ${editTarget?.name || ''}`"
      width="640px"
      ok-text="保存"
      cancel-text="取消"
      :confirm-loading="saving"
      @ok="handleEditSave"
    >
      <a-form v-if="editTarget" layout="vertical" :model="editForm">
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="名称" required>
              <a-input v-model:value="editForm.name" placeholder="请输入标准名称" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="编号" required>
              <a-input v-model:value="editForm.code" placeholder="请输入标准编号" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="行业">
              <a-select v-model:value="editForm.industry" :options="industrySelectOptions" allow-clear placeholder="请选择行业" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="性质">
              <a-select v-model:value="editForm.nature" :options="natureSelectOptions" allow-clear placeholder="请选择性质" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="级别">
              <a-select v-model:value="editForm.level" :options="levelSelectOptions" allow-clear placeholder="请选择级别" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="状态">
              <a-select v-model:value="editForm.status" :options="statusSelectOptions" allow-clear placeholder="请选择状态" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="发布部门">
              <a-input v-model:value="editForm.issuer" placeholder="请输入发布部门" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="发布年份">
              <a-select v-model:value="editForm.publishYear" :options="yearSelectOptions" allow-clear placeholder="请选择发布年份" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="上传人">
              <a-input v-model:value="editForm.uploader" placeholder="请输入上传人" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-form-item label="简介">
          <a-textarea v-model:value="editForm.description" :rows="4" placeholder="请输入标准简介" />
        </a-form-item>
      </a-form>
    </a-modal>

    <StandardUploadModal
      v-model:open="uploadModalVisible"
      :tasks="uploadTasks"
      @add-files="addFiles"
      @update-form="updateForm"
      @remove-task="removeTask"
      @retry-task="retryTask"
      @submit="handleUploadSubmit"
    />

    <StandardUploadTasksDrawer
      v-model:open="uploadDrawerVisible"
      :tasks="uploadTasks"
      @retry-task="retryTask"
    />

    <StandardBatchParseModal
      v-model:open="batchParseVisible"
      :items="batchParseItems"
      @view="handleBatchView"
      @retry="retryBatchParseItem"
    />
  </div>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  CloudUploadOutlined,
  InfoCircleOutlined,
  ReloadOutlined,
  UploadOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import StandardPdfViewer from './components/StandardPdfViewer.vue'
import StandardUploadModal from './components/StandardUploadModal.vue'
import StandardUploadTasksDrawer from './components/StandardUploadTasksDrawer.vue'
import StandardBatchParseModal from './components/StandardBatchParseModal.vue'
import { useStandardUpload } from './composables/useStandardUpload'
import {
  industryOptions,
  industrySelectOptions,
  levelOptions,
  levelSelectOptions,
  natureOptions,
  natureSelectOptions,
  statusColor,
  statusOptions,
  statusSelectOptions,
  yearOptions,
  yearSelectOptions,
} from './constants'
import type { StandardParseBatchItem } from './types'
import type { StandardAIAnalysis, StandardParseBatchResult, StandardProperty } from '@/types'
import {
  deleteStandard,
  deleteStandards,
  getStandardFileUrl,
  getStandards,
  parseStandard,
  parseStandards,
  updateStandard,
} from '@/api/modules/standards'

const pageSize = 15
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const refreshing = ref(false)
const error = ref('')
const standards = ref<StandardProperty[]>([])

const query = reactive({
  keyword: '',
  industry: undefined as string | undefined,
  nature: undefined as string | undefined,
  level: undefined as string | undefined,
  status: '现行' as string | undefined,
  publishYear: undefined as number | undefined,
})

const columnWidths = reactive<Record<string, number>>({
  index: 80,
  name: 240,
  code: 200,
  industry: 100,
  nature: 90,
  level: 120,
  status: 100,
  publishYear: 100,
  uploader: 120,
  action: 180,
})

const columnMinWidths: Record<string, number> = {
  index: 60,
  name: 240,
  code: 140,
  industry: 80,
  nature: 70,
  level: 100,
  status: 80,
  publishYear: 80,
  uploader: 100,
  action: 180,
}

const columns = computed(() => [
  { title: '序号', dataIndex: 'index', key: 'index', width: columnWidths.index, minWidth: columnMinWidths.index, resizable: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: columnWidths.name, minWidth: columnMinWidths.name, resizable: true },
  { title: '编号', dataIndex: 'code', key: 'code', width: columnWidths.code, minWidth: columnMinWidths.code, resizable: true },
  { title: '行业', dataIndex: 'industry', key: 'industry', width: columnWidths.industry, minWidth: columnMinWidths.industry, resizable: true },
  { title: '性质', dataIndex: 'nature', key: 'nature', width: columnWidths.nature, minWidth: columnMinWidths.nature, resizable: true },
  { title: '级别', dataIndex: 'level', key: 'level', width: columnWidths.level, minWidth: columnMinWidths.level, resizable: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: columnWidths.status, minWidth: columnMinWidths.status, resizable: true },
  { title: '发布年份', dataIndex: 'publishYear', key: 'publishYear', width: columnWidths.publishYear, minWidth: columnMinWidths.publishYear, resizable: true },
  { title: '上传人', dataIndex: 'uploader', key: 'uploader', width: columnWidths.uploader, minWidth: columnMinWidths.uploader, resizable: true },
  { title: '操作', key: 'action', width: columnWidths.action, minWidth: columnMinWidths.action, fixed: 'right', resizable: true },
])

const tableContainerRef = ref<HTMLElement | null>(null)
const containerWidth = ref(0)
let tableResizeObserver: ResizeObserver | undefined

const contentWidth = computed(() =>
  columns.value.reduce((sum, col) => sum + (typeof col.width === 'number' ? col.width : 0), 0),
)
const scrollX = computed(() => Math.max(containerWidth.value, contentWidth.value))

function observeTableWidth(): void {
  if (!tableContainerRef.value) return
  tableResizeObserver = new ResizeObserver((entries) => {
    const width = entries[0]?.contentRect.width
    if (width) containerWidth.value = Math.round(width)
  })
  tableResizeObserver.observe(tableContainerRef.value)
}

function handleResizeColumn(width: number, column: { key?: string }): void {
  const key = column.key
  if (!key || !(key in columnWidths)) return
  const minWidth = columnMinWidths[key] ?? 50
  columnWidths[key] = Math.max(minWidth, Math.round(width))
}

let filterTimer: ReturnType<typeof setTimeout> | undefined

watch(query, () => {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    page.value = 1
    fetchStandards()
  }, 300)
}, { deep: true })

async function fetchStandards(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const res = await getStandards({
      keyword: query.keyword.trim() || undefined,
      industry: query.industry,
      nature: query.nature,
      level: query.level,
      status: query.status,
      publishYear: query.publishYear,
      skipCount: (page.value - 1) * pageSize,
      maxResultCount: pageSize,
    })
    standards.value = res.items
    total.value = res.totalCount
  } catch {
    error.value = '标准列表加载失败，请稍后重试'
  } finally {
    loading.value = false
  }
}

function handleReset(): void {
  query.keyword = ''
  query.industry = undefined
  query.nature = undefined
  query.level = undefined
  query.status = '现行'
  query.publishYear = undefined
}

async function handleRefresh(): Promise<void> {
  refreshing.value = true
  await fetchStandards()
  refreshing.value = false
  if (error.value) {
    message.error('刷新失败')
  } else {
    message.success('已刷新')
  }
}

interface TablePagination {
  current?: number
}

function handleTableChange(paginationInfo: TablePagination): void {
  page.value = paginationInfo.current || 1
  fetchStandards()
}

const viewerVisible = ref(false)
const viewerTarget = ref<StandardProperty | null>(null)
const viewerPage = ref(1)

function openViewer(record: StandardProperty): void {
  viewerTarget.value = record
  viewerPage.value = 1
  viewerVisible.value = true
}

function resetViewer(): void {
  viewerTarget.value = null
  viewerPage.value = 1
}

const editVisible = ref(false)
const editTarget = ref<StandardProperty | null>(null)
const saving = ref(false)
const editForm = reactive<Partial<StandardProperty>>({
  name: '',
  code: '',
  industry: undefined,
  nature: undefined,
  level: undefined,
  status: undefined,
  issuer: undefined,
  publishYear: undefined,
  uploader: undefined,
  description: '',
})

function openEdit(target: StandardProperty | null): void {
  if (!target) return
  editTarget.value = target
  editForm.name = target.name
  editForm.code = target.code
  editForm.industry = target.industry
  editForm.nature = target.nature
  editForm.level = target.level
  editForm.status = target.status
  editForm.issuer = target.issuer
  editForm.publishYear = target.publishYear
  editForm.uploader = target.uploader
  editForm.description = target.description
  editVisible.value = true
}

async function handleEditSave(): Promise<void> {
  if (!editTarget.value) return
  if (!editForm.name?.trim() || !editForm.code?.trim()) {
    message.warning('请填写名称和编号')
    return
  }
  saving.value = true
  try {
    const updated = await updateStandard(editTarget.value.id, { ...editForm })
    const idx = standards.value.findIndex((s) => s.id === updated.id)
    if (idx !== -1) standards.value[idx] = updated
    if (viewerTarget.value?.id === updated.id) viewerTarget.value = updated
    editVisible.value = false
    message.success('保存成功')
    fetchStandards()
  } catch {
    message.error('保存失败')
  } finally {
    saving.value = false
  }
}

const parseVisible = ref(false)
const parseLoading = ref(false)
const parseError = ref('')
const parseResult = ref<StandardAIAnalysis | null>(null)
const parseTarget = ref<StandardProperty | null>(null)

async function openParse(record: StandardProperty): Promise<void> {
  parseTarget.value = record
  parseResult.value = null
  parseError.value = ''
  parseVisible.value = true
  parseLoading.value = true
  try {
    parseResult.value = await parseStandard(record.id)
  } catch {
    parseError.value = '解析失败，请稍后重试'
  } finally {
    parseLoading.value = false
  }
}

function resetParse(): void {
  parseResult.value = null
  parseError.value = ''
  parseTarget.value = null
}

// ── 批量操作 ─────────────────────────────────────────────
const selectedRowKeys = ref<Array<string | number>>([])

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: Array<string | number>) => {
    selectedRowKeys.value = keys
  },
}))

async function handleDelete(record: StandardProperty): Promise<void> {
  try {
    await deleteStandard(record.id)
    standards.value = standards.value.filter((s) => s.id !== record.id)
    total.value -= 1
    selectedRowKeys.value = selectedRowKeys.value.filter((key) => key !== record.id)
    message.success('删除成功')
    if (standards.value.length === 0 && page.value > 1) {
      page.value -= 1
      await fetchStandards()
    }
  } catch {
    message.error('删除失败')
  }
}

async function handleBatchDelete(): Promise<void> {
  const ids = selectedRowKeys.value.map(String)
  if (!ids.length) return
  try {
    const count = await deleteStandards(ids)
    selectedRowKeys.value = []
    message.success(`删除成功 ${count} 条`)
    await fetchStandards()
    if (standards.value.length === 0 && page.value > 1) {
      page.value -= 1
      await fetchStandards()
    }
  } catch {
    message.error('批量删除失败')
  }
}

const batchParseVisible = ref(false)
const batchParseItems = ref<StandardParseBatchItem[]>([])
const batchParseAnalyses = ref<Record<string, StandardAIAnalysis>>({})

function openBatchParse(): void {
  const byId = new Map(standards.value.map((s) => [s.id, s]))
  batchParseItems.value = selectedRowKeys.value.map(String).map((id) => {
    const record = byId.get(id)
    return { id, name: record?.name ?? id, status: 'parsing' as const }
  })
  if (!batchParseItems.value.length) return
  batchParseVisible.value = true
  void runBatchParse()
}

function applyParseResult(result: StandardParseBatchResult): void {
  const item = batchParseItems.value.find((i) => i.id === result.id)
  if (!item) return
  if (result.success) {
    item.status = 'success'
    item.error = undefined
    if (result.analysis) batchParseAnalyses.value[result.id] = result.analysis
  } else {
    item.status = 'failed'
    item.error = result.error
  }
}

async function runBatchParse(): Promise<void> {
  try {
    const results = await parseStandards(batchParseItems.value.map((i) => i.id), applyParseResult)
    const successCount = results.filter((r) => r.success).length
    const failedCount = results.length - successCount
    if (failedCount === 0) message.success(`批量解析完成，共 ${successCount} 条`)
    else message.warning(`批量解析完成：成功 ${successCount} 条，失败 ${failedCount} 条`)
    await fetchStandards()
  } catch {
    message.error('批量解析失败，请重试')
  }
}

async function retryBatchParseItem(id: string): Promise<void> {
  const item = batchParseItems.value.find((i) => i.id === id)
  if (!item) return
  item.status = 'parsing'
  item.error = undefined
  await parseStandards([id], applyParseResult)
}

function handleBatchView(id: string): void {
  const analysis = batchParseAnalyses.value[id]
  const record = standards.value.find((s) => s.id === id)
  if (!analysis || !record) {
    message.warning('未找到解析结果')
    return
  }
  parseTarget.value = record
  parseResult.value = analysis
  parseError.value = ''
  parseVisible.value = true
}

// ── 上传文档 ─────────────────────────────────────────────
const uploadModalVisible = ref(false)
const uploadDrawerVisible = ref(false)

const {
  tasks: uploadTasks,
  runningCount: uploadRunningCount,
  hasTasks: uploadHasTasks,
  addFiles,
  removeTask,
  retryTask,
  submitUploads,
  updateForm,
  dispose: disposeUpload,
} = useStandardUpload(() => {
  void fetchStandards()
})

function handleUploadSubmit(): void {
  uploadModalVisible.value = false
  void submitUploads()
}

onMounted(() => {
  observeTableWidth()
  fetchStandards()
})

onBeforeUnmount(() => {
  tableResizeObserver?.disconnect()
  disposeUpload()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.page-container :deep(.page-header-left) {
  display: flex;
  align-items: baseline;
  gap: @spacing-sm;
}
.page-container :deep(.page-desc) {
  margin-top: 0;
  color: @text-tertiary;
}
.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}

.standards-filter-bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  flex-wrap: wrap;
  margin-bottom: @spacing-base;
}
.standards-filter-bar__reset {
  margin-left: 0;
}
.standards-filter-bar__refresh {
  margin-left: auto;
}
.standards-header-refresh {
  display: none;
}

.standards-table-wrap {
  min-width: 0;
}

.standards-batch-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-md;
  padding: @spacing-sm @spacing-base;
  border-bottom: 1px solid @border-color;

  &__count {
    font-size: @font-size-sm;
    color: @text-secondary;
  }
}

.action-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 2px;
  white-space: nowrap;
}

.viewer-body {
  height: 100%;
  min-height: 0;
  padding: @spacing-md;
}

.parse-result {
  padding: @spacing-md @spacing-lg;
}
.parse-result__summary {
  margin: 0 0 @spacing-lg;
  line-height: 1.7;
  color: @text-primary;
}
.parse-result__block {
  margin-bottom: @spacing-lg;
  &:last-child {
    margin-bottom: 0;
  }
}
.parse-result__title {
  margin: 0 0 @spacing-sm;
  font-size: @font-size-base;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
.parse-result__list {
  margin: 0;
  padding-left: @spacing-lg;
  color: @text-secondary;
  line-height: 1.7;
  li + li {
    margin-top: @spacing-xs;
  }
  &--risk {
    color: @danger;
  }
}
</style>
