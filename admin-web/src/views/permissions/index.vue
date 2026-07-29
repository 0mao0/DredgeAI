<template>
  <div class="page-container">
    <PageHeader title="权限管理" description="管理系统角色和权限" />

    <SectionCard title="角色列表" flush>
      <template #extra>
        <a-button type="primary" size="small" @click="openCreateModal">新增角色</a-button>
      </template>
      <a-table
        :data-source="roles"
        :columns="columns"
        :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'index'">
            {{ record._index }}
          </template>
          <template v-else-if="column.key === 'appCount'">
            <a-tag v-if="record.appIds.includes('*')" color="blue">全部</a-tag>
            <span v-else>{{ record.appIds.length }} 项</span>
          </template>
          <template v-else-if="column.key === 'users'">
            <a-button type="link" size="small" @click="openDrawer(record)">{{ record.userCount }} 人</a-button>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" @click="openDrawer(record)">编辑</a-button>
            <a-popconfirm
              title="确认删除该角色？"
              placement="left"
              @confirm="handleDelete(record.id)"
            >
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </SectionCard>

    <a-modal
      v-model:open="formModalVisible"
      :title="'新增角色'"
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
        <a-button type="primary" size="small" :loading="savingAll" @click="handleSaveAll">保存</a-button>
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
            :loading="drawerLoading"
            @add="handleAddRoleUser"
            @remove="handleRemoveRoleUser"
          />
        </a-tab-pane>
        <a-tab-pane key="menus" :tab="menuTabLabel">
          <RoleMenuTab
            :checked-keys="drawerPendingMenuKeys"
            :tree="menuPermTree"
            @change="(keys: string[]) => { drawerPendingMenuKeys = keys }"
          />
        </a-tab-pane>
        <a-tab-pane key="apps" :tab="appTabLabel">
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
import { ref, computed, onMounted, h } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { Role, OrgUser, ApplicationItem } from '@/types'
import {
  getRoles, createRole, updateRole, deleteRole,
  getRoleUsers, addRoleUsers, removeRoleUser, setRolePermissions,
} from '@/api/modules/roles'
import { getApplications } from '@/api/modules/applications'
import { adminAppManifests, adminMenuGroups } from '@/router/manifests'
import { manifestToMenu } from '@shared/web/router/manifest'
import type { MenuNode } from '@shared/web/router/manifest'
import RoleUserTab from './components/RoleUserTab.vue'
import RoleMenuTab from './components/RoleMenuTab.vue'
import RoleAppTab from './components/RoleAppTab.vue'
import type { PermTreeNode } from './types'

const loading = ref(false)
const roles = ref<(Role & { _index?: number })[]>([])

const columns = [
  { title: '序号', key: 'index', width: 80 },
  { title: '角色', dataIndex: 'name', width: 120 },
  { title: '应用权限', key: 'appCount', width: 100 },
  { title: '人员', key: 'users', width: 90 },
  { title: '创建时间', dataIndex: 'createdAt', width: 120 },
  { title: '操作', key: 'action', width: 160 },
]

async function fetchRoles(): Promise<void> {
  loading.value = true
  try {
    const list = await getRoles()
    roles.value = list.map((r, i) => ({ ...r, _index: i + 1 }))
  } catch {
    message.error('加载角色列表失败')
  } finally {
    loading.value = false
  }
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
    await createRole({ name: formName.value })
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

const drawerVisible = ref(false)
const drawerRole = ref<Role | null>(null)
const drawerTab = ref('users')
const drawerRoleUsers = ref<OrgUser[]>([])
const drawerLoading = ref(false)
const drawerFormName = ref('')
const savingAll = ref(false)
const drawerPendingMenuKeys = ref<string[]>([])
const drawerPendingAppIds = ref<string[]>([])

const userTabLabel = computed(() => `人员 (${drawerRole.value?.userCount ?? 0})`)
const menuTabLabel = computed(() => {
  const keys = drawerRole.value?.menuKeys
  if (!keys) return '菜单权限 (0)'
  if (keys.includes('*')) return '菜单权限 (全部)'
  return `菜单权限 (${keys.length})`
})
const appTabLabel = computed(() => {
  const ids = drawerRole.value?.appIds
  if (!ids) return '应用权限 (0)'
  if (ids.includes('*')) return '应用权限 (全部)'
  return `应用权限 (${ids.length})`
})

async function openDrawer(role: Role): Promise<void> {
  drawerRole.value = role
  drawerFormName.value = role.name
  drawerPendingMenuKeys.value = [...role.menuKeys]
  drawerPendingAppIds.value = [...role.appIds]
  drawerTab.value = 'users'
  drawerVisible.value = true
  await fetchDrawerUsers()
}

async function fetchDrawerUsers(): Promise<void> {
  if (!drawerRole.value) return
  drawerLoading.value = true
  try {
    drawerRoleUsers.value = await getRoleUsers(drawerRole.value.id)
  } catch {
    message.error('加载人员失败')
  } finally {
    drawerLoading.value = false
  }
}

async function handleAddRoleUser(userIds: string[]): Promise<void> {
  if (!drawerRole.value) return
  try {
    await addRoleUsers(drawerRole.value.id, userIds)
    message.success('已添加')
    drawerRole.value.userCount += userIds.length
    fetchRoles()
    fetchDrawerUsers()
  } catch {
    message.error('添加失败')
  }
}

async function handleRemoveRoleUser(userId: string): Promise<void> {
  if (!drawerRole.value) return
  try {
    await removeRoleUser(drawerRole.value.id, userId)
    message.success('已移除')
    drawerRole.value.userCount--
    fetchRoles()
    fetchDrawerUsers()
  } catch {
    message.error('移除失败')
  }
}

const menuPermTree = computed<PermTreeNode[]>(() => {
  const menuTree = manifestToMenu(adminAppManifests, adminMenuGroups)
  return menuToPermTree(menuTree)
})

function menuToPermTree(nodes: MenuNode[]): PermTreeNode[] {
  return nodes.map((n) => ({
    title: n.title,
    key: n.key,
    children: n.children ? menuToPermTree(n.children) : undefined,
    selectable: !n.children || n.children.length === 0,
  }))
}

const appTreeLoading = ref(false)
const apps = ref<ApplicationItem[]>([])

const appPermTree = computed<PermTreeNode[]>(() => {
  const catColorMap: Record<string, string> = {
    通用: '#3B82F6',
    经营: '#10B981',
    设计: '#8B5CF6',
    施工: '#F59E0B',
  }
  const catOrder = ['通用', '经营', '设计', '施工']

  const catGroups = new Map<string, ApplicationItem[]>()
  for (const app of apps.value) {
    const cat = app.category || '通用'
    if (!catGroups.has(cat)) catGroups.set(cat, [])
    catGroups.get(cat)!.push(app)
  }

  const catLabel = (cat: string) => {
    const color = catColorMap[cat] || '#94A3B8'
    return h('span', {
      class: 'cat-tag-inline',
      style: { color, borderColor: color, background: `${color}22` },
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

async function handleSaveAll(): Promise<void> {
  if (!drawerRole.value || !drawerFormName.value.trim()) return
  savingAll.value = true
  try {
    await Promise.all([
      updateRole(drawerRole.value.id, { name: drawerFormName.value.trim() }),
      setRolePermissions(drawerRole.value.id, {
        menuKeys: drawerPendingMenuKeys.value,
        appIds: drawerPendingAppIds.value,
      }),
    ])
    drawerRole.value.name = drawerFormName.value.trim()
    drawerRole.value.menuKeys = [...drawerPendingMenuKeys.value]
    drawerRole.value.appIds = [...drawerPendingAppIds.value]
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
