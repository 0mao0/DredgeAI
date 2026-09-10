<template>
  <div class="page-container">
    <PageHeader title="组织用户" description="用户与组织结构管理">
      <template #extra>
        <AppButton v-if="can('create')" variant="primary" size="sm" @click="openCreateModal">新增用户</AppButton>
      </template>
    </PageHeader>

    <DataTable
      v-model:query="query"
      :columns="columns"
      :data-source="users"
      row-key="id"
      :loading="loading"
      :pagination="{ current: page, pageSize, total }"
      :filters="filters"
      @change="handleTableChange"
    >
      <template #toolbarExtra>
        <AppButton size="sm" :loading="refreshing" @click="handleRefresh">
          <ReloadOutlined />
          刷新
        </AppButton>
      </template>
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'index'">
          {{ (page - 1) * pageSize + index + 1 }}
        </template>
        <template v-else-if="column.key === 'departments'">
          <span>{{ record.organizationUnits.map((u: OrgUnitBrief) => u.name).join('、') || '—' }}</span>
        </template>
        <template v-else-if="column.key === 'isActive'">
          <a-popconfirm
            v-if="can('update')"
            :title="record.isActive ? '确认限制该用户登录？' : '确认允许该用户登录？'"
            placement="left"
            @confirm="toggleStatus(record)"
          >
            <a-checkbox :checked="record.isActive" />
          </a-popconfirm>
          <a-checkbox v-else :checked="record.isActive" disabled />
        </template>
        <template v-else-if="column.key === 'roles'">
          <div class="role-cell">
            <span class="role-cell__tags">
              <template v-if="record.roleNames && record.roleNames.length">
                <a-tag v-for="name in record.roleNames" :key="name" color="blue" class="role-tag">{{ name }}</a-tag>
              </template>
              <span v-else class="no-role-label">未分配</span>
            </span>
            <AppButton v-if="can('update')" variant="link" size="sm" class="role-set-btn" @click="openRoleModal(record)"><SettingOutlined /></AppButton>
          </div>
        </template>
        <template v-else-if="column.key === 'action'">
          <div class="action-cell">
            <AppButton v-if="can('update')" variant="link" size="sm" @click="openEditModal(record)">编辑</AppButton>
            <a-popconfirm
              v-if="can('delete')"
              title="确认删除该用户？"
              placement="left"
              @confirm="handleDelete(record)"
            >
              <AppButton variant="link" size="sm" danger>删除</AppButton>
            </a-popconfirm>
          </div>
        </template>
      </template>
    </DataTable>

    <a-modal
      v-model:open="roleModalVisible"
      :title="`分配角色 — ${currentUser?.name}`"
      width="440px"
      @ok="handleSaveRoles"
    >
      <a-checkbox-group v-model:value="selectedRoleNames" class="role-group">
        <a-checkbox v-for="r in allRoles" :key="r.id" :value="r.name">{{ r.name }}</a-checkbox>
      </a-checkbox-group>
    </a-modal>

    <a-modal
      v-model:open="formModalVisible"
      :title="editingUser ? '编辑用户' : '新增用户'"
      :width="440"
      :confirm-loading="saving"
      @ok="handleSaveForm"
    >
      <a-form ref="formRef" :model="form" :rules="formRules" layout="vertical">
        <a-form-item v-if="!editingUser" label="用户名" name="userName">
          <a-input v-model:value="form.userName" placeholder="登录用户名" />
        </a-form-item>
        <a-form-item label="姓名" name="name">
          <a-input v-model:value="form.name" placeholder="请输入姓名" />
        </a-form-item>
        <a-form-item label="手机号" name="phoneNumber">
          <a-input v-model:value="form.phoneNumber" placeholder="请输入手机号" />
        </a-form-item>
        <a-form-item v-if="!editingUser" label="初始密码" name="password">
          <a-input-password v-model:value="form.password" placeholder="请输入初始密码" />
        </a-form-item>
        <a-form-item label="部门" name="organizationIds">
          <a-tree-select
            v-model:value="form.organizationIds"
            :tree-data="orgTree"
            :field-names="{ children: 'children', label: 'displayName', value: 'id' }"
            multiple
            allow-clear
            tree-default-expand-all
            placeholder="请选择部门"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { ref, computed, onMounted, reactive, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import { ReloadOutlined, SettingOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import {
  getOrgUsers,
  setUserActive,
  updateUserRoles,
  getAllRoleOptions,
  createOrgUser,
  updateOrgUser,
  deleteOrgUser,
} from '@/api/modules/org-users'
import type { OrgUserItem, OrgUnitBrief, RoleOption } from '@/api/modules/org-users'
import { getOrgUnitTree } from '@/api/modules/org-units'
import type { OrgUnit } from '@/types'
import { usePagePermissions } from '@/composables/usePagePermissions'

const { can } = usePagePermissions()
const pageSize = 15
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const refreshing = ref(false)
const users = ref<OrgUserItem[]>([])
const allRoles = ref<RoleOption[]>([])

const orgTree = ref<OrgUnit[]>([])
const query = ref({ keyword: '', status: undefined as string | undefined })

const filters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索姓名或手机号', width: 240 },
  {
    key: 'status',
    type: 'select',
    placeholder: '状态',
    width: 110,
    options: [
      { value: 'true', label: '启用' },
      { value: 'false', label: '禁用' },
    ],
  },
]

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '序号', dataIndex: 'index', key: 'index', width: 80, minWidth: 60, resizable: true },
    { title: '姓名', dataIndex: 'name', key: 'name', width: 120, minWidth: 100, resizable: true },
    { title: '手机', dataIndex: 'phoneNumber', key: 'phoneNumber', width: 140, minWidth: 120, resizable: true },
    { title: '激活', dataIndex: 'isActive', key: 'isActive', width: 80, minWidth: 70, resizable: true },
    { title: '部门', key: 'departments', width: 220, minWidth: 180, resizable: true },
    // 操作列固定右侧；相邻“角色”列不参与拖拽（fixed-right 浮层会盖住其手柄）
    { title: '角色', key: 'roles', width: 160, minWidth: 140 },
    { title: '操作', key: 'action', width: 120, minWidth: 120, fixed: 'right', resizable: true },
  ]
  return can('update') || can('delete') ? base : base.filter((c) => c.key !== 'action')
})

let filterTimer: ReturnType<typeof setTimeout> | undefined

watch(query, () => {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    page.value = 1
    void fetchUsers()
  }, 300)
}, { deep: true })

async function fetchUsers(): Promise<void> {
  loading.value = true
  try {
    const res = await getOrgUsers({
      keyword: query.value.keyword?.trim() || undefined,
      isActive: query.value.status === undefined ? undefined : query.value.status === 'true',
      skipCount: (page.value - 1) * pageSize,
      maxResultCount: pageSize,
    })
    users.value = res.items
    total.value = res.totalCount
  } catch {
    message.error('加载用户列表失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(paginationInfo: unknown): void {
  page.value = (paginationInfo as { current?: number } | null)?.current || 1
  fetchUsers()
}

async function handleRefresh(): Promise<void> {
  refreshing.value = true
  try {
    allRoles.value = await getAllRoleOptions()
    orgTree.value = await getOrgUnitTree()
    await fetchUsers()
    message.success('已刷新')
  } catch {
    message.error('刷新失败')
  } finally {
    refreshing.value = false
  }
}

async function toggleStatus(user: OrgUserItem): Promise<void> {
  const next = !user.isActive
  try {
    await setUserActive(user.id, next)
    user.isActive = next
    message.success(next ? '已允许登录' : '已限制登录')
  } catch {
    message.error('操作失败')
  }
}

const roleModalVisible = ref(false)
const currentUser = ref<OrgUserItem | null>(null)
const selectedRoleNames = ref<string[]>([])

function openRoleModal(user: OrgUserItem): void {
  currentUser.value = user
  selectedRoleNames.value = [...user.roleNames]
  roleModalVisible.value = true
}

async function handleSaveRoles(): Promise<void> {
  if (!currentUser.value) return
  try {
    await updateUserRoles(currentUser.value.id, selectedRoleNames.value)
    currentUser.value.roleNames = [...selectedRoleNames.value]
    roleModalVisible.value = false
    message.success('角色已更新')
  } catch {
    message.error('保存失败')
  }
}

const formModalVisible = ref(false)
const editingUser = ref<OrgUserItem | null>(null)
const saving = ref(false)
const formRef = ref<FormInstance>()
const form = reactive({
  userName: '',
  name: '',
  phoneNumber: '',
  password: '',
  organizationIds: [] as string[],
})
const formRules = {
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  phoneNumber: [
    { required: true, message: '请输入手机号', trigger: 'blur' },
    { pattern: /^1[3-9]\d{9}$/, message: '手机号格式不正确', trigger: 'blur' },
  ],
  password: [{ required: true, message: '请输入初始密码', trigger: 'blur' }],
}

function openCreateModal(): void {
  editingUser.value = null
  form.userName = ''
  form.name = ''
  form.phoneNumber = ''
  form.password = ''
  form.organizationIds = []
  formModalVisible.value = true
}

function openEditModal(user: OrgUserItem): void {
  editingUser.value = user
  form.name = user.name
  form.phoneNumber = user.phoneNumber ?? ''
  form.organizationIds = user.organizationUnits.map((u) => u.key)
  formModalVisible.value = true
}

async function handleSaveForm(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  saving.value = true
  try {
    if (editingUser.value) {
      await updateOrgUser(editingUser.value.id, {
        name: form.name.trim(),
        phoneNumber: form.phoneNumber.trim(),
        organizationIds: form.organizationIds,
      })
      message.success('已保存')
    } else {
      await createOrgUser({
        userName: form.userName.trim(),
        name: form.name.trim(),
        phoneNumber: form.phoneNumber.trim(),
        password: form.password,
        organizationIds: form.organizationIds,
      })
      message.success('已创建')
    }
    formModalVisible.value = false
    await fetchUsers()
  } catch {
    message.error(editingUser.value ? '保存失败' : '创建失败')
  } finally {
    saving.value = false
  }
}

async function handleDelete(user: OrgUserItem): Promise<void> {
  try {
    await deleteOrgUser(user.id)
    message.success('已删除')
    if (users.value.length === 1 && page.value > 1) page.value -= 1
    await fetchUsers()
  } catch {
    message.error('删除失败')
  }
}

onMounted(async () => {
  allRoles.value = await getAllRoleOptions()
  orgTree.value = await getOrgUnitTree()
  fetchUsers()
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

.role-cell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-sm;
}
.role-cell__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 2px;
  flex: 1;
  min-width: 0;
}

.role-tag {
  font-size: @font-size-xs;
}

.role-set-btn {
  flex-shrink: 0;
  padding: 0;
  font-size: 14px;
}

.no-role-label {
  color: @text-tertiary;
  font-size: @font-size-xs;
}

.role-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.action-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  white-space: nowrap;
}
</style>
