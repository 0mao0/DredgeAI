<template>
  <div class="page-container">
    <PageHeader title="组织用户" description="用户与组织结构管理" />

    <SectionCard nopad class="org-users-card">
      <div class="table-top-bar">
        <a-input-search
          v-model:value="searchKeyword"
          placeholder="搜索姓名或手机号"
          style="width: 240px"
          allow-clear
          @search="handleSearch"
        />
        <AppButton size="sm" :loading="refreshing" @click="handleRefresh">
          <ReloadOutlined />
          刷新
        </AppButton>
      </div>
      <a-table
        :data-source="users"
        :columns="columns"
        :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'departments'">
            <span>{{ record.departments.join('、') }}</span>
          </template>
          <template v-else-if="column.key === 'roles'">
            <div class="role-cell">
              <span class="role-cell__tags">
                <template v-if="record._roleNames && record._roleNames.length">
                  <a-tag v-for="r in record._roleNames" :key="r" color="blue" class="role-tag">{{ r }}</a-tag>
                </template>
                <span v-else class="no-role-label">未分配</span>
              </span>
              <AppButton variant="link" size="sm" class="role-set-btn" @click="openRoleModal(record)"><SettingOutlined /></AppButton>
            </div>
          </template>
          <template v-else-if="column.key === 'action'">
            <div class="action-cell">
              <a-popconfirm
                :title="record.status === 'active' ? '确认限制该用户登录？' : '确认允许该用户登录？'"
                placement="left"
                @confirm="toggleStatus(record)"
              >
                <a-switch
                  :checked="record.status === 'disabled'"
                  checked-children="允许登录"
                  un-checked-children="限制登录"
                />
              </a-popconfirm>
            </div>
          </template>
        </template>
      </a-table>
    </SectionCard>

    <a-modal
      v-model:open="roleModalVisible"
      :title="`分配角色 — ${currentUser?.name}`"
      width="440px"
      @ok="handleSaveRoles"
    >
      <a-checkbox-group v-model:value="selectedRoleIds" class="role-group">
        <a-checkbox v-for="r in allRoles" :key="r.id" :value="r.id">{{ r.name }}</a-checkbox>
      </a-checkbox-group>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { ReloadOutlined, SettingOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { OrgUser, Role } from '@/types'
import { getOrgUsers, setUserStatus, setUserRoles } from '@/api/modules/org-users'
import { getRoles } from '@/api/modules/roles'

const loading = ref(false)
const refreshing = ref(false)
const searchKeyword = ref('')
const users = ref<(OrgUser & { _roleNames?: string[] })[]>([])
const allRoles = ref<Role[]>([])

const columns = [
  { title: '序号', dataIndex: 'index', width: 80 },
  { title: '姓名', dataIndex: 'name', width: 100 },
  { title: '手机', dataIndex: 'phone', width: 140 },
  { title: '部门', key: 'departments', width: 220 },
  { title: '角色', key: 'roles' },
  { title: '操作', key: 'action', width: 120 },
]

function resolveRoleNames(roleIds: string[]): string[] {
  return roleIds.map((rid) => allRoles.value.find((r) => r.id === rid)?.name || rid)
}

async function fetchUsers(): Promise<void> {
  loading.value = true
  try {
    const res = await getOrgUsers({ keyword: searchKeyword.value || undefined })
    users.value = res.items.map((u, i) => ({
      ...u,
      index: i + 1,
      _roleNames: resolveRoleNames(u.roleIds),
    }))
  } catch {
    message.error('加载用户列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch(): void {
  fetchUsers()
}

async function handleRefresh(): Promise<void> {
  refreshing.value = true
  try {
    allRoles.value = await getRoles()
    await fetchUsers()
    message.success('已刷新')
  } catch {
    message.error('刷新失败')
  } finally {
    refreshing.value = false
  }
}

async function toggleStatus(user: OrgUser & { _roleNames?: string[] }): Promise<void> {
  const newStatus = user.status === 'active' ? 'disabled' : 'active'
  try {
    await setUserStatus(user.id, newStatus)
    user.status = newStatus
    message.success(newStatus === 'active' ? '已启用' : '已禁用')
  } catch {
    message.error('操作失败')
  }
}

const roleModalVisible = ref(false)
const currentUser = ref<(OrgUser & { _roleNames?: string[] }) | null>(null)
const selectedRoleIds = ref<string[]>([])

function openRoleModal(user: OrgUser & { _roleNames?: string[] }): void {
  currentUser.value = user
  selectedRoleIds.value = [...user.roleIds]
  roleModalVisible.value = true
}

async function handleSaveRoles(): Promise<void> {
  if (!currentUser.value) return
  try {
    await setUserRoles(currentUser.value.id, selectedRoleIds.value)
    currentUser.value.roleIds = [...selectedRoleIds.value]
    currentUser.value._roleNames = resolveRoleNames(selectedRoleIds.value)
    roleModalVisible.value = false
    message.success('角色已更新')
  } catch {
    message.error('保存失败')
  }
}

onMounted(async () => {
  allRoles.value = await getRoles()
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

.table-top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: @spacing-sm @spacing-xl;
  border-bottom: 1px solid @border-color;
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

.action-cell :deep(.ant-switch) {
  vertical-align: middle;
}
.action-cell :deep(.ant-switch-handle) {
  top: 2px;
}

.action-cell :deep(.ant-switch-checked) {
  background-color: @success;
}
.action-cell :deep(.ant-switch:not(.ant-switch-checked)) {
  background-color: @danger;
}
</style>
