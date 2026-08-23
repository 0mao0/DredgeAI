<template>
  <div ref="tableContainerRef" class="data-table">
    <div v-if="hasToolbar" class="data-table-toolbar">
      <slot v-if="$slots.toolbar" name="toolbar" />
      <div v-else class="data-table-filter-bar">
        <template v-for="f in filters" :key="f.key">
          <a-input
            v-if="f.type === 'input'"
            v-model:value="localQuery[f.key]"
            :placeholder="f.placeholder"
            allow-clear
            :style="{ width: `${f.width || 220}px` }"
            @change="emitQuery"
          />
          <a-select
            v-else-if="f.type === 'select'"
            v-model:value="localQuery[f.key]"
            :placeholder="f.placeholder"
            allow-clear
            :mode="f.multiple ? 'multiple' : undefined"
            :max-tag-count="f.multiple ? 0 : undefined"
            :max-tag-placeholder="f.multiple ? `已选 ${(localQuery[f.key] as string[] | undefined)?.length ?? 0}` : undefined"
            :style="{ width: `${f.width || (f.multiple ? 160 : 120)}px` }"
            @change="emitQuery"
          >
            <a-select-option v-for="opt in normalizedOptions(f)" :key="String(opt.value)" :value="opt.value">
              {{ opt.label }}
            </a-select-option>
          </a-select>
          <a-radio-group
            v-else-if="f.type === 'radio'"
            v-model:value="localQuery[f.key]"
            button-style="solid"
            size="small"
            @change="emitQuery"
          >
            <a-radio-button v-for="opt in normalizedOptions(f)" :key="String(opt.value)" :value="opt.value">
              {{ opt.label }}
            </a-radio-button>
          </a-radio-group>
          <div v-else class="data-table-filter-switch">
            <span v-if="f.label" class="data-table-filter-switch__label">{{ f.label }}</span>
            <a-switch
              v-model:checked="localQuery[f.key]"
              size="small"
              :checked-children="f.checkedLabel"
              :un-checked-children="f.uncheckedLabel"
              @change="emitQuery"
            />
          </div>
        </template>
        <div v-if="$slots.toolbarExtra" class="data-table-filter-bar__extra">
          <slot name="toolbarExtra" />
        </div>
      </div>
    </div>

    <SectionCard v-if="card" nopad class="data-table-card">
      <slot name="tableExtra" />
      <a-table
        class="data-table__table"
        :columns="effectiveColumns"
        :data-source="dataSource"
        :row-key="rowKey"
        :loading="loading"
        :pagination="paginationProps"
        :scroll="{ x: scrollX }"
        :locale="{ emptyText }"
        :expandable="expandable"
        :row-selection="rowSelection"
        size="small"
        @resize-column="handleResizeColumn"
        @change="onTableChange"
      >
        <template #bodyCell="scope">
          <slot name="bodyCell" v-bind="scope" />
        </template>
        <template v-if="$slots.expandedRowRender" #expandedRowRender="scope">
          <slot name="expandedRowRender" v-bind="scope" />
        </template>
        <template v-if="$slots.emptyText" #emptyText>
          <slot name="emptyText" />
        </template>
      </a-table>
    </SectionCard>
    <template v-else>
      <slot name="tableExtra" />
      <a-table
        class="data-table__table"
        :columns="effectiveColumns"
        :data-source="dataSource"
        :row-key="rowKey"
        :loading="loading"
        :pagination="paginationProps"
        :scroll="{ x: scrollX }"
        :locale="{ emptyText }"
        :expandable="expandable"
        :row-selection="rowSelection"
        size="small"
        @resize-column="handleResizeColumn"
        @change="onTableChange"
      >
        <template #bodyCell="scope">
          <slot name="bodyCell" v-bind="scope" />
        </template>
        <template v-if="$slots.expandedRowRender" #expandedRowRender="scope">
          <slot name="expandedRowRender" v-bind="scope" />
        </template>
        <template v-if="$slots.emptyText" #emptyText>
          <slot name="emptyText" />
        </template>
      </a-table>
    </template>
  </div>
</template>

<script lang="ts">
</script>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, useSlots, watch } from 'vue'
import SectionCard from './SectionCard.vue'

export interface DataTableColumn {
  key?: string
  width?: number
  minWidth?: number
  resizable?: boolean
  fixed?: 'left' | 'right' | boolean
  [key: string]: unknown
}

export interface DataTableFilter {
  key: string
  type: 'input' | 'select' | 'switch' | 'radio'
  placeholder?: string
  width?: number
  options?: Array<string | number | { value: string | number, label: string }>
  /** select 多选模式 */
  multiple?: boolean
  /** switch 类型显示的标签 */
  label?: string
  /** switch 选中/未选中文案 */
  checkedLabel?: string
  uncheckedLabel?: string
}

const props = withDefaults(defineProps<{
  columns: DataTableColumn[]
  dataSource: Record<string, any>[]
  rowKey: string | ((record: Record<string, any>) => string)
  loading?: boolean
  pagination?: Record<string, any> | boolean
  expandable?: Record<string, any>
  rowSelection?: Record<string, any>
  filters?: DataTableFilter[]
  query?: Record<string, any>
  card?: boolean
  fillWidth?: boolean
  emptyText?: string
}>(), {
  loading: false,
  pagination: false,
  filters: () => [],
  card: true,
  fillWidth: true,
  emptyText: '暂无数据',
})

const emit = defineEmits<{
  'update:query': [q: Record<string, any>]
  'change': [pagination: unknown, filters: unknown, sorter: unknown]
}>()

// ── 列宽拖拽 ────────────────────────────────────────────────
const internalWidths = reactive<Record<string, number>>({})
const columnMinWidths: Record<string, number> = {}

watch(() => props.columns, (cols) => {
  for (const col of cols) {
    if (!col.resizable || !col.key) continue
    if (!(col.key in internalWidths)) {
      internalWidths[col.key] = typeof col.width === 'number' ? col.width : 100
    }
    columnMinWidths[col.key] = typeof col.minWidth === 'number' ? col.minWidth : 50
  }
}, { immediate: true, deep: true })

const effectiveColumns = computed<DataTableColumn[]>(() =>
  props.columns.map((col) => {
    if (!col.resizable || !col.key) return col
    return { ...col, width: internalWidths[col.key] ?? col.width, minWidth: col.minWidth }
  }),
)

function handleResizeColumn(width: number, column: { key?: string }): void {
  const key = column.key
  if (!key || !(key in internalWidths)) return
  internalWidths[key] = Math.max(columnMinWidths[key] ?? 50, Math.round(width))
}

// ── 横向自适应：表格宽度跟随容器，列总宽小于容器时按比例摊开填满 ──
const tableContainerRef = ref<HTMLElement | null>(null)
const containerWidth = ref(0)
let tableResizeObserver: ResizeObserver | undefined

const contentWidth = computed(() =>
  effectiveColumns.value.reduce((sum, col) => sum + (typeof col.width === 'number' ? col.width : 0), 0),
)
const scrollX = computed(() => Math.max(containerWidth.value, contentWidth.value))

function fillWidthToContainer(): void {
  if (!props.fillWidth) return
  const el = tableContainerRef.value
  if (!el) return
  const width = el.clientWidth
  if (!width) return
  const total = contentWidth.value
  if (total === 0 || width <= total) return
  const scale = width / total
  for (const key of Object.keys(internalWidths)) {
    internalWidths[key] = Math.max(columnMinWidths[key] ?? 50, Math.round(internalWidths[key] * scale))
  }
}

function observeTableWidth(): void {
  if (!tableContainerRef.value) return
  tableResizeObserver = new ResizeObserver((entries) => {
    const width = entries[0]?.contentRect.width
    if (!width) return
    containerWidth.value = Math.round(width)
    if (width > contentWidth.value) fillWidthToContainer()
  })
  tableResizeObserver.observe(tableContainerRef.value)
}

onMounted(() => {
  observeTableWidth()
  fillWidthToContainer()
})

onBeforeUnmount(() => {
  tableResizeObserver?.disconnect()
})

// ── 分页约定：默认 showSizeChanger=false + showTotal ──
const paginationProps = computed(() => {
  if (!props.pagination || typeof props.pagination !== 'object') return false
  return {
    showSizeChanger: false,
    ...props.pagination,
    showTotal: props.pagination.showTotal ?? ((t: number) => `共 ${t} 条`),
  }
})

function onTableChange(pagination: unknown, filters: unknown, sorter: unknown): void {
  emit('change', pagination, filters, sorter)
}

// ── 筛选栏：配置驱动，v-model:query ──
const hasToolbar = computed(() =>
  props.filters.length > 0 || !!(useSlots().toolbar || useSlots().toolbarExtra),
)

const localQuery = reactive<Record<string, any>>({})

watch(() => props.query, (q) => {
  if (q) Object.assign(localQuery, q)
}, { deep: true, immediate: true })

function normalizedOptions(f: DataTableFilter): Array<{ value: string | number, label: string }> {
  return (f.options ?? []).map((o) => {
    if (typeof o === 'object' && o !== null) return o
    return { value: o, label: String(o) }
  })
}

function emitQuery(): void {
  const q: Record<string, any> = {}
  for (const f of props.filters) {
    const v = localQuery[f.key]
    q[f.key] = v === '' ? undefined : v
  }
  emit('update:query', q)
}
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.data-table-filter-bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  flex-wrap: wrap;
  margin-bottom: @spacing-base;

  &__extra {
    display: inline-flex;
    align-items: center;
    gap: @spacing-sm;
    margin-left: auto;
  }
}

.data-table-filter-switch {
  display: inline-flex;
  align-items: center;
  gap: @spacing-sm;

  &__label {
    font-size: @font-size-sm;
    white-space: nowrap;
    color: @text-secondary;
  }
}

// 覆盖 rc-table 内联 min-width: 100% 与自动布局，
// 避免列总宽小于容器时被浏览器等比拉伸，保证拖拽调宽时表头线与鼠标位移一致
.data-table__table :deep(table) {
  min-width: auto !important;
  table-layout: fixed !important;
}
</style>
