<template>
  <div class="role-user-tab">
    <div class="role-user-tab__header">
      <a-input-search
        v-model:value="searchText"
        placeholder="搜索用户姓名"
        style="width: 200px"
        allow-clear
      />
      <AppButton variant="primary" size="sm" @click="showAddModal = true">新增人员</AppButton>
    </div>

    <a-table
      :data-source="filteredUsers"
      :columns="columns"
      :pagination="{ pageSize: 10, showTotal: (t: number) => `共 ${t} 条` }"
      :loading="loading"
      row-key="id"
      size="small"
    >
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
    </a-table>

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
import { AppButton } from '@shared/web'
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

const filteredUsers = computed(() => {
  if (!searchText.value) return props.roleUsers
  const kw = searchText.value.toLowerCase()
  return props.roleUsers.filter((u) => u.name.includes(kw))
})

const columns = [
  { title: '姓名', dataIndex: 'name' },
  { title: '手机', dataIndex: 'phone', width: 140 },
  { title: '部门', key: 'departments', width: 200 },
  { title: '操作', key: 'action', width: 80 },
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

.role-user-tab {
  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: @spacing-base;
  }
}

.user-check-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>
