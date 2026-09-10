<template>
  <div class="page-container">
    <PageHeader title="权限管理" description="管理系统角色和权限">
      <template #extra>
        <AppButton v-if="can('create')" variant="primary" size="sm" @click="openCreateModal">新增角色</AppButton>
      </template>
    </PageHeader>

    <div class="permissions-table-wrap">
      <DataTable
        :columns="columns"
        :data-source="roles"
        row-key="id"
        :pagination="{ current: page, pageSize, total }"
        :loading="loading"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.key === 'index'">
            {{ (page - 1) * pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'appCount'">
            <span class="app-count-pending">暂未对接</span>
          </template>
          <template v-else-if="column.key === 'users'">
            <AppButton variant="link" size="sm" @click="openDrawer(record)">{{ record.userCount }} 人</AppButton>
          </template>
          <template v-else-if="column.key === 'action'">
            <AppButton v-if="can('update')" variant="link" size="sm" @click="openDrawer(record)">编辑</AppButton>
            <a-popconfirm
              v-if="can('delete')"
              title="确认删除该角色？"
              placement="left"
              @confirm="handleDelete(record.id)"
            >
              <AppButton variant="link" size="sm" danger>删除</AppButton>
            </a-popconfirm>
          </template>
        </template>
      </DataTable>
    </div>

    <a-modal
      v-model:open="formModalVisible"
      title="新增角色"
      width="440px"
      @ok="handleSaveForm"
    >
      <a-form layout="vertical">
        <a-form-item label="角色名称">
          <a-input v-model:value="formName" placeholder="请输入角色名称" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer
      :open="drawerVisible"
      title="角色详情"
      width="640px"
      @close="drawerVisible = false"
    >
      <template #extra>
        <AppButton v-if="canSaveAll" variant="primary" size="sm" :loading="savingAll" @click="handleSaveAll">保存</AppButton>
      </template>
      <template v-if="drawerRole">
        <div class="drawer-name-row">
          <span class="drawer-name-label">角色名称：</span>
          <a-input v-model:value="drawerFormName" style="width: 280px" />
        </div>
        <a-tabs v-model:active-key="drawerTab" class="drawer-tabs">
          <a-tab-pane key="users" :tab="userTabLabel">
            <RoleUserTab
              :role="drawerRole"
              :role-users="drawerRoleUsers"
              :addable-users="addableUsers"
              :loading="drawerLoading"
              :can-manage-users="can('manageUsers')"
              @add="handleAddRoleUser"
              @remove="handleRemoveRoleUser"
            />
          </a-tab-pane>
          <a-tab-pane key="menus" :tab="menuTabLabel">
            <RoleMenuTab
              :checked-keys="drawerPendingMenuKeys"
              :tree="menuPermTree"
              @change="(keys: string[], half: string[]) => { drawerPendingMenuKeys = keys; drawerHalfCheckedKeys = half }"
            />
          </a-tab-pane>
          <a-tab-pane key="apps" :tab="appTabLabel">
            <a-alert
              type="info"
              show-icon
              message="应用权限暂未对接，勾选仅当前会话有效，保存不会提交"
              style="margin-bottom: 12px"
            />
            <RoleAppTab
              :checked-keys="drawerPendingAppIds"
              :tree="appPermTree"
              :loading="appTreeLoading"
              @change="(keys: string[]) => { drawerPendingAppIds = keys }"
            />
          </a-tab-pane>
        </a-tabs>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import { ref, computed, onMounted, h } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import type { ApplicationItem } from '@/types'
import type { AppManifest } from '@shared/core/types/application'
import {
  getRoles,
  createRole,
  updateRole,
  deleteRole,
  getRoleUserCounts,
  getRoleUsers,
  setRoleUsers,
  removeRoleUser,
  getRoleGrantedPermissions,
  setRolePermissions,
} from '@/api/modules/roles'
import type { RoleItem } from '@/api/modules/roles'
import { getOrgUsers } from '@/api/modules/org-users'
import type { OrgUserItem } from '@/api/modules/org-users'
import { getApplications } from '@/api/modules/applications'
import { adminAppManifests, adminMenuGroups } from '@/router/manifests'
import { manifestToMenu } from '@shared/web/router/manifest'
import type { MenuNode } from '@shared/web/router/manifest'
import { getCategoryColor, getCategoryAlphaBg } from '@shared/core/utils'
import { formatDateTime, resolveAppConfigTimeZone } from '@shared/web/utils/format'
import { useAppStore } from '@/stores/app'
import RoleUserTab from './components/RoleUserTab.vue'
import RoleMenuTab from './components/RoleMenuTab.vue'
import RoleAppTab from './components/RoleAppTab.vue'
import type { PermTreeNode } from './types'
import { usePagePermissions } from '@/composables/usePagePermissions'

const { can } = usePagePermissions()
/** 抽屉「保存」同时提交角色名（updateRole）与菜单权限（setRolePermissions），任一权限可用即可保存 */
const canSaveAll = computed(() => can('update') || can('managePermissions'))

const appStore = useAppStore()
/** 显示时区：应用配置 timing.timeZone（ABP 时钟为 UTC），缺省 Asia/Shanghai */
const displayTz = computed(() => resolveAppConfigTimeZone(appStore.appConfig))

const loading = ref(false)
const roles = ref<RoleItem[]>([])
const page = ref(1)
const pageSize = 15
const total = ref(0)

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '序号', key: 'index', width: 80 },
    { title: '角色', dataIndex: 'name', key: 'name', width: 140, minWidth: 120, resizable: true },
    { title: '应用权限', key: 'appCount', width: 110, minWidth: 90, resizable: true },
    { title: '人员', key: 'users', width: 90, minWidth: 80, resizable: true },
    { title: '创建时间', dataIndex: 'creationTime', key: 'creationTime', width: 170, minWidth: 150 },
    // 操作列固定右侧；相邻“创建时间”列不参与拖拽（fixed-right 浮层会盖住其手柄）
    { title: '操作', key: 'action', width: 160, minWidth: 160, fixed: 'right', resizable: true },
  ]
  return can('update') || can('delete') ? base : base.filter((c) => c.key !== 'action')
})

async function fetchRoles(): Promise<void> {
  loading.value = true
  try {
    const [res, counts] = await Promise.all([
      getRoles({ skipCount: (page.value - 1) * pageSize, maxResultCount: pageSize }),
      getRoleUserCounts(),
    ])
    const countMap = new Map(counts.map((c) => [c.roleName, c.userCount]))
    roles.value = res.items.map((r) => ({
      ...r,
      creationTime: formatDateTime(r.creationTime, displayTz.value),
      userCount: countMap.get(r.name) ?? 0,
    }))
    total.value = res.totalCount
  } catch {
    message.error('加载角色列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(paginationInfo: unknown): void {
  page.value = (paginationInfo as { current?: number } | null)?.current || 1
  fetchRoles()
}

const formModalVisible = ref(false)
const formName = ref('')

function openCreateModal(): void {
  formName.value = ''
  formModalVisible.value = true
}

async function handleSaveForm(): Promise<void> {
  if (!formName.value.trim()) {
    message.warning('请输入角色名称')
    return
  }
  try {
    await createRole(formName.value.trim())
    message.success('已创建')
    formModalVisible.value = false
    fetchRoles()
  } catch {
    message.error('操作失败')
  }
}

async function handleDelete(id: string): Promise<void> {
  try {
    await deleteRole(id)
    message.success('已删除')
    fetchRoles()
  } catch {
    message.error('删除失败')
  }
}

// ---- 菜单权限树 ----

/** 按钮/操作标识 → 树节点标题 */
const ACTION_LABELS: Record<string, string> = {
  create: '新增',
  update: '编辑',
  delete: '删除',
  managePermissions: '分配权限',
  manageUsers: '分配人员',
}

/** manifest 递归拍平：route → manifest（查 requiredPermission / actionPermissions） */
const manifestByRoute = computed<Map<string, AppManifest>>(() => {
  const map = new Map<string, AppManifest>()
  const walk = (list: AppManifest[]): void => {
    for (const m of list) {
      map.set(m.route, m)
      if (m.children) walk(m.children)
    }
  }
  walk(adminAppManifests)
  return map
})

function menuNodeToPermNode(n: MenuNode): PermTreeNode {
  if (n.children && n.children.length > 0) {
    return {
      title: n.title,
      key: `__group__${n.key}`,
      children: n.children.map(menuNodeToPermNode),
      disableCheckbox: true,
    }
  }
  const manifest = manifestByRoute.value.get(n.key)
  const perm = manifest?.requiredPermission
  if (!perm) {
    // 未定义权限码的菜单只展示不可勾选
    return { title: n.title, key: `__noperm__${n.key}`, disableCheckbox: true }
  }
  const actions = manifest?.actionPermissions
  const actionNodes: PermTreeNode[] = actions
    ? Object.entries(actions).map(([action, code]) => ({
        title: ACTION_LABELS[action] ?? action,
        key: code,
      }))
    : []
  return {
    title: n.title,
    key: perm,
    children: actionNodes.length > 0 ? actionNodes : undefined,
  }
}

const menuPermTree = computed<PermTreeNode[]>(() =>
  manifestToMenu(adminAppManifests, adminMenuGroups).map(menuNodeToPermNode),
)

/** 树中全部真实权限码（key 不以 __ 开头） */
const knownPermCodes = computed<Set<string>>(() => {
  const codes = new Set<string>()
  const walk = (nodes: PermTreeNode[]): void => {
    for (const n of nodes) {
      if (!n.key.startsWith('__')) codes.add(n.key)
      if (n.children) walk(n.children)
    }
  }
  walk(menuPermTree.value)
  return codes
})

// ---- 应用权限树（暂未对接，仅本地态渲染） ----

const appTreeLoading = ref(false)
const apps = ref<ApplicationItem[]>([])

const appPermTree = computed<PermTreeNode[]>(() => {
  const catOrder = ['通用', '经营', '设计', '施工']

  const catGroups = new Map<string, ApplicationItem[]>()
  for (const app of apps.value) {
    const cat = app.category || '通用'
    if (!catGroups.has(cat)) catGroups.set(cat, [])
    catGroups.get(cat)!.push(app)
  }

  const catLabel = (cat: string) => {
    const color = getCategoryColor(cat)
    return h('span', {
      class: 'cat-tag-inline',
      style: { color, borderColor: color, background: getCategoryAlphaBg(cat) },
    }, cat)
  }

  const appLabel = (cat: string, name: string) =>
    h('span', { class: 'app-tree-label' }, [catLabel(cat), h('span', { class: 'app-name-text' }, name)])

  const makeChildren = (cat: string, app: ApplicationItem): PermTreeNode[] => {
    const subs = app.subApps || []
    if (subs.length === 0) return []
    return subs.map((sub) => ({
      title: appLabel(cat, sub.name),
      key: sub.id,
      selectable: true,
    }))
  }

  const nodes: PermTreeNode[] = []
  const seenCats = new Set<string>()
  for (const cat of catOrder) {
    const group = catGroups.get(cat)
    if (!group || group.length === 0) continue
    seenCats.add(cat)
    nodes.push({
      title: catLabel(cat),
      key: `__cat__${cat}`,
      selectable: false,
      children: group.map((app) => {
        const subs = app.subApps || []
        return {
          title: appLabel(cat, app.name),
          key: app.id,
          selectable: subs.length === 0,
          children: subs.length > 0 ? makeChildren(cat, app) : undefined,
        }
      }),
    })
  }
  for (const [cat, group] of catGroups) {
    if (seenCats.has(cat)) continue
    nodes.push({
      title: catLabel(cat),
      key: `__cat__${cat}`,
      selectable: false,
      children: group.map((app) => ({
        title: appLabel(cat, app.name),
        key: app.id,
        selectable: !app.subApps || app.subApps.length === 0,
        children: app.subApps ? makeChildren(cat, app) : undefined,
      })),
    })
  }
  return nodes
})

// ---- 抽屉 ----

const drawerVisible = ref(false)
const drawerRole = ref<RoleItem | null>(null)
const drawerTab = ref('users')
const drawerRoleUsers = ref<OrgUserItem[]>([])
const allUsers = ref<OrgUserItem[]>([])
const drawerLoading = ref(false)
const drawerFormName = ref('')
const savingAll = ref(false)
const drawerPendingMenuKeys = ref<string[]>([])
const drawerHalfCheckedKeys = ref<string[]>([])
const drawerPendingAppIds = ref<string[]>([])

/** 已勾选 + 半选（部分按钮被勾的菜单）权限码全集 ∩ 已知权限码 */
const drawerGrantedCodes = computed<Set<string>>(() => {
  const granted = new Set<string>()
  for (const c of [...drawerPendingMenuKeys.value, ...drawerHalfCheckedKeys.value]) {
    if (knownPermCodes.value.has(c)) granted.add(c)
  }
  return granted
})

/** 添加人员弹框候选：全量用户排除当前角色成员 */
const addableUsers = computed<OrgUserItem[]>(() => {
  const memberIds = new Set(drawerRoleUsers.value.map((u) => u.id))
  return allUsers.value.filter((u) => !memberIds.has(u.id))
})

const userTabLabel = computed(() => `人员 (${drawerRole.value?.userCount ?? 0})`)
const menuTabLabel = computed(() => `菜单权限 (${drawerGrantedCodes.value.size})`)
const appTabLabel = computed(() => '应用权限 (未对接)')

async function openDrawer(role: RoleItem): Promise<void> {
  drawerRole.value = role
  drawerFormName.value = role.name
  drawerPendingMenuKeys.value = []
  drawerHalfCheckedKeys.value = []
  drawerPendingAppIds.value = []
  drawerTab.value = 'users'
  drawerVisible.value = true
  drawerLoading.value = true
  try {
    const [users, granted, all] = await Promise.all([
      getRoleUsers(role.name),
      getRoleGrantedPermissions(role.name),
      getOrgUsers({ maxResultCount: 1000 }),
    ])
    drawerRoleUsers.value = users
    drawerPendingMenuKeys.value = granted.filter((c) => knownPermCodes.value.has(c))
    allUsers.value = all.items
  } catch {
    message.error('加载角色详情失败')
  } finally {
    drawerLoading.value = false
  }
}

/** 人员增删后刷新成员列表与列表页 userCount（drawerRole 指向列表记录，需同步换新引用） */
async function refreshDrawerUsers(): Promise<void> {
  const role = drawerRole.value
  if (!role) return
  drawerRoleUsers.value = await getRoleUsers(role.name)
  await fetchRoles()
  const updated = roles.value.find((r) => r.id === role.id)
  if (updated) drawerRole.value = updated
}

async function handleAddRoleUser(userIds: string[]): Promise<void> {
  const role = drawerRole.value
  if (!role) return
  try {
    await setRoleUsers(role.name, [...drawerRoleUsers.value.map((u) => u.id), ...userIds])
    message.success('已添加')
    await refreshDrawerUsers()
  } catch {
    message.error('添加失败')
  }
}

async function handleRemoveRoleUser(userId: string): Promise<void> {
  const role = drawerRole.value
  if (!role) return
  try {
    await removeRoleUser(role.name, userId)
    message.success('已移除')
    await refreshDrawerUsers()
  } catch {
    message.error('移除失败')
  }
}

/**
 * 保存：先改名后授权 —— ABP 角色授权 providerKey 为角色名，改名后必须用新名提交授权。
 * 应用权限不对接，不提交。
 */
async function handleSaveAll(): Promise<void> {
  const role = drawerRole.value
  const newName = drawerFormName.value.trim()
  if (!role || !newName) return
  savingAll.value = true
  try {
    if (newName !== role.name) await updateRole(role, newName)
    await setRolePermissions(
      newName,
      [...knownPermCodes.value].map((name) => ({ name, isGranted: drawerGrantedCodes.value.has(name) })),
    )
    role.name = newName
    message.success('已保存')
    fetchRoles()
  } catch {
    message.error('保存失败')
  } finally {
    savingAll.value = false
  }
}

onMounted(async () => {
  fetchRoles()
  appTreeLoading.value = true
  try {
    apps.value = await getApplications()
  } catch {
    message.error('加载应用列表失败')
  } finally {
    appTreeLoading.value = false
  }
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
.app-count-pending {
  color: @text-tertiary;
}
.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}

.drawer-name-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-base;
}
.drawer-name-label {
  white-space: nowrap;
  color: @text-primary;
  font-weight: @font-weight-semibold;
}

.drawer-tabs :deep(.ant-tabs-nav) {
  margin-bottom: @spacing-sm;
}
.drawer-tabs :deep(.ant-tabs-tab) {
  padding: 6px 10px;
}
</style>

<style lang="less">
@import '@shared/web/styles/variables.less';

.app-tree-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.cat-tag-inline {
  display: inline-flex;
  align-items: center;
  padding: 0 5px;
  height: 18px;
  line-height: 16px;
  font-size: 11px;
  font-weight: 600;
  border: 1px solid;
  border-radius: 3px;
  white-space: nowrap;
}
.app-name-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
