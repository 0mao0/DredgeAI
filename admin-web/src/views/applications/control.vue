<template>
  <div class="page-container">
    <PageHeader title="发布管理" description="管理各应用及其子应用对用户端的发布状态" />

    <DataTable
      :columns="columns"
      :data-source="treeRows"
      row-key="key"
      :loading="loading"
      :pagination="false"
      :row-class-name="rowClassName"
      :expandable="{ defaultExpandAllRows: true, expandedRowKeys, onExpand }"
    >
      <template #toolbarExtra>
        <a-popconfirm
          title="将清空所有用户的个性化顺序，恢复为 admin 默认顺序，确定继续吗？"
          ok-text="确定"
          cancel-text="取消"
          @confirm="handleResetUserOrders"
        >
          <AppButton size="sm" :loading="resetting">
            <ReloadOutlined />
            重置用户顺序
          </AppButton>
        </a-popconfirm>
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'index'">
          <span :class="{ 'index--sub': record.level === 1 }">{{ record.index }}</span>
        </template>
        <template v-else-if="column.key === 'order'">
          <span :class="{ 'order-idx--sub': record.level === 1 }">{{ record.level === 0 ? record.index : '-' }}</span>
        </template>
        <template v-else-if="column.key === 'name'">
          <div class="cell-left">
            <span class="tree-name" :class="{ 'tree-name--sub': record.level === 1 }">
              <span v-if="record.level === 1" class="tree-connector" />
              <component :is="iconOptionsMap[record.icon] || iconOptionsMap.AppstoreOutlined" class="row-icon" />
              <span class="name-text">{{ record.name }}</span>
              <span v-if="record.level === 0 && hasSub(record)" class="sub-hint">（含 {{ subCount(record) }} 个子应用）</span>
            </span>
          </div>
        </template>
        <template v-else-if="column.key === 'category'">
          <a-tag :color="catColor(record.category)">{{ record.category }}</a-tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <div class="cell-center">
            <a-popconfirm
              :title="record.published ? '确认下架该应用？' : '确认发布该应用？'"
              placement="left"
              @confirm="onToggle(record, !record.published)"
            >
              <a-switch
                :checked="record.published"
                checked-children="已发布"
                un-checked-children="已下架"
                class="status-switch"
              />
            </a-popconfirm>
          </div>
        </template>
        <template v-else-if="column.key === 'scope'">
          <div class="cell-left">
            <span class="scope-tags">
              <a-tag v-for="n in roleTags(record)" :key="n" color="blue">{{ n }}</a-tag>
              <span v-if="roleTags(record).length === 0" class="no-scope">-</span>
            </span>
          </div>
        </template>
        <template v-else-if="column.key === 'setting'">
          <div class="cell-nowrap">
            <AppButton
              variant="link"
              size="sm"
              :disabled="!canMove(record, 'up') || movingId === (record.subId ?? record.appId)"
              @click="moveApp(record, 'up')"
            >
              <ArrowUpOutlined />上移
            </AppButton>
            <AppButton
              variant="link"
              size="sm"
              :disabled="!canMove(record, 'down') || movingId === (record.subId ?? record.appId)"
              @click="moveApp(record, 'down')"
            >
              <ArrowDownOutlined />下移
            </AppButton>
            <AppButton variant="link" size="sm" @click="openSetting(record)">设置</AppButton>
          </div>
        </template>
      </template>
    </DataTable>

    <a-modal v-model:open="settingVisible" :title="`${settingTarget?.name} · 设置`" @ok="saveSetting">
      <a-form layout="horizontal" :label-col="{ span: 6 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="应用类型">
          <a-select v-model:value="settingCategory" style="width:200px">
            <a-select-option v-for="c in categories" :key="c.name" :value="c.name">
              <a-tag :color="c.color">{{ c.name }}</a-tag>
            </a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="应用图标">
          <div class="icon-grid">
            <button
              v-for="opt in iconOptions"
              :key="opt.value"
              type="button"
              class="icon-cell"
              :class="{ active: settingIcon === opt.value }"
              :title="opt.value"
              @click="settingIcon = opt.value"
            >
              <component :is="opt.comp" />
            </button>
          </div>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { APP_ICONS, AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { ArrowDownOutlined, ArrowUpOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import type { ApplicationItem, Role } from '@/types'
import {
  getApplications,
  getCategoryConfig,
  getAppOrder,
  moveAppOrder,
  resetUserOrders,
  seedAppOrder,
  setSubAppStatus,
  setApplicationStatus,
  setApplicationCategory,
  setSubAppCategory,
  setApplicationIcon,
  setSubAppIcon,
} from '@/api/modules/applications'
import { sortAppsByOrder } from '@/utils/appOrder'
import { getRoles } from '@/api/modules/roles'
import type { CategoryConfig } from '@/api/modules/applications'

interface TreeRow {
  key: string
  index: string
  name: string
  category: string
  level: 0 | 1
  published: boolean
  icon: string
  parentId?: string
  appId: string
  subId?: string
}

const roles = ref<Role[]>([])

function roleTags(row: TreeRow): string[] {
  const id = row.subId || row.appId
  const names: string[] = []
  for (const r of roles.value) {
    if (r.appIds.includes('*') || r.appIds.includes(id)) {
      names.push(r.name)
    }
  }
  return names
}

const apps = ref<ApplicationItem[]>([])
const orderIds = ref<string[]>([])
const subOrders = ref<Record<string, string[]>>({})
const movingId = ref('')
const resetting = ref(false)
const movedKey = ref('')
let movedTimer: number | undefined
const ORDER_STORAGE_KEY = 'dredge-admin-app-order'
const categories = ref<CategoryConfig[]>([])
const catColorMap = computed(() => {
  const m: Record<string, string> = {}
  for (const c of categories.value) m[c.name] = c.color
  return m
})
function catColor(c: string): string {
  return catColorMap.value[c] || 'default'
}

// 图标选项与映射与 user-web 共用共享表，避免两端漂移
const iconOptions = Object.entries(APP_ICONS).map(([value, comp]) => ({ value, comp }))
const iconOptionsMap: Record<string, unknown> = APP_ICONS

const orderedApps = computed(() => {
  return sortAppsByOrder(apps.value, orderIds.value)
})

const treeRows = computed<TreeRow[]>(() => {
  const rows: TreeRow[] = []
  let appIdx = 0
  for (const app of orderedApps.value) {
    appIdx++
    const subs = app.subApps || []
    const parentOrder = subOrders.value[app.id]
    const sortedSubs = parentOrder?.length
      ? [...subs].sort((x, y) => {
          const xi = parentOrder.indexOf(x.id)
          const yi = parentOrder.indexOf(y.id)
          return (xi === -1 ? Number.MAX_SAFE_INTEGER : xi) - (yi === -1 ? Number.MAX_SAFE_INTEGER : yi)
        })
      : subs
    rows.push({
      key: `app-${app.id}`,
      index: String(appIdx),
      name: app.name,
      category: app.category,
      level: 0,
      published: app.status === '运营中',
      icon: app.icon || 'AppstoreOutlined',
      appId: app.id,
    })
    sortedSubs.forEach((sub, si) => {
      rows.push({
        key: `sub-${sub.id}`,
        index: `${appIdx}.${si + 1}`,
        name: sub.name,
        category: sub.category,
        level: 1,
        published: sub.status === '已发布',
        icon: sub.icon,
        parentId: app.id,
        appId: app.id,
        subId: sub.id,
      })
    })
  }
  return rows
})

function canMove(record: TreeRow, direction: 'up' | 'down'): boolean {
  const key = record.subId ?? record.appId
  const list = record.level === 0
    ? orderIds.value
    : (subOrders.value[record.parentId ?? ''] ?? [])
  const index = list.indexOf(key)
  if (index === -1) return true
  if (direction === 'up') return index > 0
  return index < list.length - 1
}

function subOrdersPayload(): Record<string, string[]> {
  const out: Record<string, string[]> = {}
  for (const a of apps.value) {
    if (a.subApps?.length) out[a.id] = a.subApps.map((s) => s.id)
  }
  return out
}

async function loadOrder(): Promise<void> {
  try {
    let order = await getAppOrder()
    const ids = apps.value.map((a) => a.id)
    const subsComplete = apps.value.every((a) => {
      const subIds = (a.subApps ?? []).map((s) => s.id)
      if (subIds.length === 0) return true
      const known = order.subOrders?.[a.id] ?? []
      return subIds.every((id) => known.includes(id))
    })
    if (!ids.every((id) => order.appIds.includes(id)) || !subsComplete) {
      // 后端 seed 为合并语义：保留已有位置，仅追加新应用/子应用
      order = await seedAppOrder(ids, subOrdersPayload())
    }
    orderIds.value = order.appIds
    subOrders.value = order.subOrders ?? {}
  } catch {
    // 后端顺序服务不可用：优先恢复本地保存的顺序，保证刷新后不回到目录顺序
    const local = readLocalOrder()
    if (local) {
      orderIds.value = local.appIds
      subOrders.value = local.subOrders ?? {}
    } else {
      orderIds.value = apps.value.map((a) => a.id)
      subOrders.value = subOrdersPayload()
    }
    message.warning('应用顺序服务未启动，顺序调整仅保存在当前浏览器')
  }
}

function readLocalOrder(): { appIds: string[], subOrders?: Record<string, string[]> } | null {
  try {
    const raw = localStorage.getItem(ORDER_STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as { appIds?: unknown, subOrders?: Record<string, string[]> }
    if (!Array.isArray(parsed?.appIds)) return null
    return { appIds: parsed.appIds as string[], subOrders: parsed.subOrders }
  } catch {
    return null
  }
}

function saveLocalOrder(): void {
  try {
    localStorage.setItem(ORDER_STORAGE_KEY, JSON.stringify({ appIds: orderIds.value, subOrders: subOrders.value }))
  } catch {
    // localStorage 不可用时忽略
  }
}

async function moveApp(record: TreeRow, direction: 'up' | 'down'): Promise<void> {
  if (movingId.value) return
  const key = record.subId ?? record.appId
  movingId.value = key
  try {
    const order = await moveAppOrder(key, direction)
    orderIds.value = order.appIds
    subOrders.value = order.subOrders ?? {}
  } catch {
    // 后端不可用时本地交换（子项在母项组内交换）
    if (record.level === 0) {
      const index = orderIds.value.indexOf(key)
      const target = direction === 'up' ? index - 1 : index + 1
      if (index >= 0 && target >= 0 && target < orderIds.value.length) {
        const next = [...orderIds.value]
        ;[next[index], next[target]] = [next[target], next[index]]
        orderIds.value = next
        saveLocalOrder()
      }
    } else {
      const parentId = record.parentId
      if (!parentId) return
      const list = [...(subOrders.value[parentId] ?? [])]
      const index = list.indexOf(key)
      const target = direction === 'up' ? index - 1 : index + 1
      if (index >= 0 && target >= 0 && target < list.length) {
        ;[list[index], list[target]] = [list[target], list[index]]
        subOrders.value = { ...subOrders.value, [parentId]: list }
        saveLocalOrder()
      }
    }
  } finally {
    movingId.value = ''
  }
  // 移动后短暂高亮该行，方便看到落点
  movedKey.value = record.key
  if (movedTimer) window.clearTimeout(movedTimer)
  movedTimer = window.setTimeout(() => {
    if (movedKey.value === record.key) movedKey.value = ''
  }, 2000)
}

function rowClassName(record: TreeRow): string {
  return record.key === movedKey.value ? 'row-moved' : ''
}

async function handleResetUserOrders(): Promise<void> {
  resetting.value = true
  try {
    const res = await resetUserOrders()
    message.success(`已重置 ${res.count} 个用户的个性化顺序`)
  } catch {
    message.error('重置失败，请确认后端应用顺序服务已启动')
  } finally {
    resetting.value = false
  }
}

function hasSub(row: TreeRow): boolean {
  return treeRows.value.some((r) => r.parentId === row.appId)
}
function subCount(row: TreeRow): number {
  return treeRows.value.filter((r) => r.parentId === row.appId).length
}

const expandedRowKeys = ref<string[]>([])
function onExpand(keys: string[]): void {
  expandedRowKeys.value = keys
}

const columns: DataTableColumn[] = [
  { title: '序号', key: 'index', width: 60, minWidth: 60, resizable: true },
  { title: '用户顺序', key: 'order', width: 90, minWidth: 80, resizable: true },
  { title: '应用', key: 'name', width: 200, minWidth: 150, resizable: true },
  { title: '分类', key: 'category', width: 90, minWidth: 80, resizable: true },
  { title: '状态', key: 'status', width: 90, minWidth: 80, resizable: true },
  { title: '授权角色', key: 'scope', width: 160, minWidth: 120, resizable: true },
  { title: '操作', key: 'setting', width: 210, minWidth: 210, fixed: 'right', resizable: true },
]

const loading = ref(false)

async function onToggle(row: TreeRow, val: boolean): Promise<void> {
  if (row.level === 1 && row.subId) {
    await setSubAppStatus(row.subId, val ? '已发布' : '已下架')
  } else {
    await setApplicationStatus(row.appId, val ? '运营中' : '已下架')
  }
  // 重新拉取以同步状态
  apps.value = await getApplications()
  // 展开含有子应用的行
  expandedRowKeys.value = apps.value.filter((a) => a.subApps?.length).map((a) => `app-${a.id}`)
  message.success(val ? '已发布' : '已下架')
}

const settingVisible = ref(false)
const settingTarget = ref<TreeRow | null>(null)
const settingIcon = ref<string>('AppstoreOutlined')
const settingCategory = ref<string>('')
function openSetting(row: TreeRow): void {
  settingTarget.value = row
  settingIcon.value = row.icon || 'AppstoreOutlined'
  settingCategory.value = row.category
  settingVisible.value = true
}

async function saveSetting(): Promise<void> {
  const row = settingTarget.value
  if (!row) return
  const subId = row.level === 1 ? row.subId : undefined
  await Promise.all([
    subId ? setSubAppIcon(subId, settingIcon.value) : setApplicationIcon(row.appId, settingIcon.value),
    settingCategory.value !== row.category
      ? (subId ? setSubAppCategory(subId, settingCategory.value) : setApplicationCategory(row.appId, settingCategory.value))
      : Promise.resolve(),
  ])
  apps.value = await getApplications()
  expandedRowKeys.value = apps.value.filter((a) => a.subApps?.length).map((a) => `app-${a.id}`)
  settingVisible.value = false
  message.success('已保存')
}

onMounted(async () => {
  loading.value = true
  try {
    const [appData, catData, roleData] = await Promise.all([getApplications(), getCategoryConfig(), getRoles()])
    apps.value = appData
    categories.value = catData
    roles.value = roleData
    await loadOrder()
    expandedRowKeys.value = apps.value.filter((a) => a.subApps?.length).map((a) => `app-${a.id}`)
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.tree-name { display: inline-flex; align-items: center; gap: @spacing-sm; }
.tree-name--sub { color: @text-secondary; }
.index--sub { color: @text-tertiary; }
.order-idx--sub { color: @text-tertiary; }
.row-icon { font-size: 14px; color: @text-secondary; }
.name-text { font-weight: 500; }
.sub-hint { font-size: @font-size-xs; color: @text-tertiary; }

// 移动后的行高亮（与 hover 同款底色 + 首列品牌色边），1.6s 后自动淡出
:deep(.row-moved) {
  > td {
    // 比 hover（@surface-hover）更强：品牌色 14% 叠加卡片底色
    background: color-mix(in srgb, @brand-primary 14%, @card-bg) !important;
    transition: background 0.3s ease;
  }
  > td:first-child {
    box-shadow: inset 3px 0 0 @brand-primary;
  }
}

.tree-connector {
  display: inline-block;
  width: 14px;
  height: 1px;
  margin-right: 4px;
  border-bottom: 1px solid @border-color;
  position: relative;
  &::before {
    content: '';
    position: absolute;
    left: 0;
    top: -10px;
    width: 1px;
    height: 11px;
    border-left: 1px solid @border-color;
  }
}

.setting-tip { color: @text-secondary; }
.cell-nowrap {
  white-space: nowrap;
  // antd 对 icon + span 默认 8px 间距，操作列按钮收紧
  :deep(.ant-btn .anticon + span) {
    margin-left: 2px;
  }
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
}
.icon-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 40px;
  font-size: 18px;
  color: @text-secondary;
  background: @content-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  cursor: pointer;
  transition: all @transition-fast;

  &:hover { border-color: var(--color-brand); color: var(--color-brand); }
  &.active {
    border-color: var(--color-brand);
    color: #fff;
    background: var(--color-brand);
    box-shadow: @shadow-sm;
  }
}

.scope-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 2px;
  justify-content: center;
}
.no-scope {
  color: @text-tertiary;
}
.cell-center {
  display: flex;
  align-items: center;
  justify-content: center;
}
.cell-left {
  display: flex;
  align-items: center;
  justify-content: flex-start;
}

.status-switch.ant-switch-checked {
  background-color: @success;
}
.status-switch:not(.ant-switch-checked) {
  background-color: @border-color;
}
</style>
