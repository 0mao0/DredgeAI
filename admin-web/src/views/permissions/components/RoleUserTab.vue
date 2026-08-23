<template>
  <div class="role-user-tab">
    <DataTable
      v-model:query="query"
      storage-key="admin-role-user-tab"
      :columns="columns"
      :data-source="filteredUsers"
      row-key="id"
      :pagination="{ pageSize: 10 }"
      :loading="loading"
      :filters="filters"
      :card="false"
    >
      <template #toolbarExtra>
        <AppButton variant="primary" size="sm" @click="showAddModal = true">新增人员</AppButton>
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'departments'">
          <a-tag v-for="d in record.departments" :key="d" color="default">{{ d }}</a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-popconfirm
            title="确认从该角色移除该用户？"
            placement="left"
            @confirm="emit('remove', record.id)"
          >
            <AppButton variant="link" size="sm" danger>移除</AppButton>
          </a-popconfirm>
        </template>
      </template>
    </DataTable>

    <a-modal
      v-model:open="showAddModal"
      title="添加人员到角色"
      width="440px"
      @ok="handleAdd"
    >
      <a-checkbox-group v-model:value="selectedUserIds" class="user-check-group">
        <a-checkbox v-for="u in addableUsers" :key="u.id" :value="u.id">
          {{ u.name }} — {{ u.departments.join(', ') }}
        </a-checkbox>
      </a-checkbox-group>
      <a-empty v-if="addableUsers.length === 0" description="没有可添加的用户" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import type { OrgUser, Role } from '@/types'

const props = defineProps<{
  role: Role
  roleUsers: OrgUser[]
  loading: boolean
}>()

const emit = defineEmits<{
  add: [userIds: string[]]
  remove: [userId: string]
}>()

const searchText = ref('')

const filters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索用户姓名', width: 200 },
]

const query = computed({
  get: () => ({ keyword: searchText.value }),
  set: (v: { keyword: string }) => { searchText.value = v.keyword ?? '' },
})

const filteredUsers = computed(() => {
  if (!searchText.value) return props.roleUsers
  const kw = searchText.value.toLowerCase()
  return props.roleUsers.filter((u) => u.name.includes(kw))
})

const columns: DataTableColumn[] = [
  { title: '姓名', dataIndex: 'name', key: 'name', width: 120, minWidth: 100, resizable: true },
  { title: '手机', dataIndex: 'phone', key: 'phone', width: 140, minWidth: 120, resizable: true },
  { title: '部门', key: 'departments', width: 200, minWidth: 160, resizable: true },
  // 不固定右侧：fixed-right 浮层会盖住相邻可拖拽列（部门）的手柄
  { title: '操作', key: 'action', width: 90, minWidth: 90, resizable: true },
]

const showAddModal = ref(false)
const addableUsers = ref<OrgUser[]>([])
const selectedUserIds = ref<string[]>([])

async function handleAdd(): Promise<void> {
  if (selectedUserIds.value.length === 0) {
    message.warning('请选择用户')
    return
  }
  emit('add', selectedUserIds.value)
  showAddModal.value = false
  selectedUserIds.value = []
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.user-check-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>
