<template>
  <div class="page-container">
    <PageHeader title="发布管理" description="管理各应用及其子应用对用户端的发布状态" />

    <a-table
      :columns="columns"
      :data-source="treeRows"
      row-key="key"
      :pagination="false"
      size="small"
      :expandable="{ defaultExpandAllRows: true, expandedRowKeys, onExpand }"
      class="publish-tree"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'name'">
          <span class="tree-name" :class="{ 'tree-name--sub': record.level === 1 }">
            <span v-if="record.level === 1" class="tree-connector" />
            <component :is="iconOptionsMap[record.icon] || iconOptionsMap.AppstoreOutlined" class="row-icon" />
            <a-tag :color="catColor(record.category)">{{ record.category }}</a-tag>
            <span class="name-text">{{ record.name }}</span>
            <span v-if="record.level === 0 && hasSub(record)" class="sub-hint">（含 {{ subCount(record) }} 个子应用）</span>
          </span>
        </template>
        <template v-else-if="column.key === 'status'">
          <a-switch
            :checked="record.published"
            checked-children="已发布"
            un-checked-children="已下架"
            @change="(v: boolean) => onToggle(record, v)"
          />
        </template>
        <template v-else-if="column.key === 'scope'">
          <a-tag
            class="scope-tag"
            :color="record.scope === '部分' ? 'orange' : 'default'"
            @click="openScope(record)"
          >
            {{ record.scope }}<EditOutlined class="scope-edit" />
          </a-tag>
        </template>
        <template v-else-if="column.key === 'setting'">
          <a-button type="link" size="small" @click="openSetting(record)">设置</a-button>
        </template>
      </template>
    </a-table>

    <a-modal v-model:open="settingVisible" :title="`${settingTarget?.name} · 设置`" @ok="saveSetting">
      <a-form layout="vertical">
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
        <p class="setting-tip">更多个性化设置将在后续版本开放。</p>
      </a-form>
    </a-modal>

    <a-modal v-model:open="scopeVisible" :title="`${scopeTarget?.name} · 授权范围`" @ok="saveScope">
      <a-form layout="vertical">
        <a-form-item label="授权方式">
          <a-radio-group v-model:value="scopeMode">
            <a-radio value="所有">所有用户</a-radio>
            <a-radio value="部分">按角色指定</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item v-if="scopeMode === '部分'" label="可见角色">
          <a-checkbox-group v-model:value="scopeRoles" class="role-group">
            <a-checkbox v-for="r in roleOptions" :key="r.value" :value="r.value">{{ r.label }}</a-checkbox>
          </a-checkbox-group>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/components/PageHeader.vue'
import type { ApplicationItem } from '@/types'
import * as Icons from '@ant-design/icons-vue'
import { EditOutlined } from '@ant-design/icons-vue'
import {
  getApplications,
  setSubAppStatus,
  setApplicationStatus,
  setApplicationIcon,
  setSubAppIcon,
  setApplicationScope,
  setSubAppScope,
} from '@/api/modules/applications'

interface TreeRow {
  key: string
  name: string
  category: string
  level: 0 | 1
  published: boolean
  icon: string
  scope: '所有' | '部分'
  parentId?: string
  appId: string
  subId?: string
}

const roleOptions = [
  { value: 'super_admin', label: '超级管理员' },
  { value: 'admin', label: '管理员' },
  { value: 'operator', label: '运营人员' },
  { value: 'engineer', label: '工程师' },
  { value: 'guest', label: '访客' },
]

const apps = ref<ApplicationItem[]>([])

const catColorMap: Record<string, string> = {
  '通用': 'blue',
  '经营': 'green',
  '设计': 'purple',
  '施工': 'gold',
}
function catColor(c: string): string {
  return catColorMap[c] || 'default'
}

const iconOptions = [
  { value: 'BookOutlined', comp: Icons.BookOutlined },
  { value: 'VideoCameraOutlined', comp: Icons.VideoCameraOutlined },
  { value: 'CustomerServiceOutlined', comp: Icons.CustomerServiceOutlined },
  { value: 'BulbOutlined', comp: Icons.BulbOutlined },
  { value: 'ToolOutlined', comp: Icons.ToolOutlined },
  { value: 'FileProtectOutlined', comp: Icons.FileProtectOutlined },
  { value: 'DashboardOutlined', comp: Icons.DashboardOutlined },
  { value: 'RadarChartOutlined', comp: Icons.RadarChartOutlined },
  { value: 'ExperimentOutlined', comp: Icons.ExperimentOutlined },
  { value: 'FileSearchOutlined', comp: Icons.FileSearchOutlined },
  { value: 'AppstoreOutlined', comp: Icons.AppstoreOutlined },
]
const iconOptionsMap: Record<string, unknown> = Object.fromEntries(iconOptions.map((o) => [o.value, o.comp]))

const treeRows = computed<TreeRow[]>(() => {
  const rows: TreeRow[] = []
  for (const app of apps.value) {
    const subs = app.subApps || []
    rows.push({
      key: `app-${app.id}`,
      name: app.name,
      category: app.category,
      level: 0,
      published: app.status === '运营中',
      icon: app.icon || 'AppstoreOutlined',
      scope: app.scope || '所有',
      appId: app.id,
    })
    for (const sub of subs) {
      rows.push({
        key: `sub-${sub.id}`,
        name: sub.name,
        category: sub.category,
        level: 1,
        published: sub.status === '已发布',
        icon: sub.icon,
        scope: sub.scope || '所有',
        parentId: app.id,
        appId: app.id,
        subId: sub.id,
      })
    }
  }
  return rows
})

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

const columns = [
  { title: '应用', key: 'name' },
  { title: '状态', key: 'status', width: 160 },
  { title: '授权范围', key: 'scope', width: 130 },
  { title: '设置', key: 'setting', width: 120 },
]

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
function openSetting(row: TreeRow): void {
  settingTarget.value = row
  settingIcon.value = row.icon || 'AppstoreOutlined'
  settingVisible.value = true
}

async function saveSetting(): Promise<void> {
  const row = settingTarget.value
  if (!row) return
  if (row.level === 1 && row.subId) {
    await setSubAppIcon(row.subId, settingIcon.value)
  } else {
    await setApplicationIcon(row.appId, settingIcon.value)
  }
  apps.value = await getApplications()
  expandedRowKeys.value = apps.value.filter((a) => a.subApps?.length).map((a) => `app-${a.id}`)
  settingVisible.value = false
  message.success('图标已更新')
}

// ---------- 授权范围 ----------
const scopeVisible = ref(false)
const scopeTarget = ref<TreeRow | null>(null)
const scopeMode = ref<'所有' | '部分'>('所有')
const scopeRoles = ref<string[]>([])
function openScope(row: TreeRow): void {
  scopeTarget.value = row
  scopeMode.value = row.scope
  scopeRoles.value = []
  scopeVisible.value = true
}
async function saveScope(): Promise<void> {
  const row = scopeTarget.value
  if (!row) return
  const scope = scopeMode.value
  if (row.level === 1 && row.subId) {
    await setSubAppScope(row.subId, scope)
  } else {
    await setApplicationScope(row.appId, scope)
  }
  apps.value = await getApplications()
  expandedRowKeys.value = apps.value.filter((a) => a.subApps?.length).map((a) => `app-${a.id}`)
  scopeVisible.value = false
  message.success(scope === '部分' ? '已设为按角色授权' : '已设为对所有用户开放')
}

onMounted(async () => {
  apps.value = await getApplications()
  expandedRowKeys.value = apps.value.filter((a) => a.subApps?.length).map((a) => `app-${a.id}`)
})
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';

.tree-name { display: inline-flex; align-items: center; gap: @spacing-sm; }
.tree-name--sub { color: @text-secondary; }
.row-icon { font-size: 14px; color: @text-secondary; }
.name-text { font-weight: 500; }
.sub-hint { font-size: @font-size-xs; color: @text-tertiary; }

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

.scope-tag { cursor: pointer; user-select: none; display: inline-flex; align-items: center; gap: 2px; }
.scope-edit { font-size: 10px; opacity: 0.6; }
.role-group { display: flex; flex-direction: column; gap: 4px; }

.publish-tree :deep(.ant-table-thead > tr > th) {
  text-align: center;
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
</style>
