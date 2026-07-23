<template>
  <div class="perm-panel">
    <div class="perm-panel__header">
      <a-radio-group v-model:value="scope" button-style="solid" size="small">
        <a-radio-button value="unauthorized">未授权人员</a-radio-button>
        <a-radio-button value="authorized">已授权人员</a-radio-button>
      </a-radio-group>
      <span class="perm-panel__count">{{ displayUsers.length }} 人</span>
    </div>
    <a-table
      :columns="columns"
      :data-source="displayUsers"
      :pagination="{ pageSize: 10, showSizeChanger: false, showTotal: (t: number) => `共 ${t} 人` }"
      row-key="id"
      size="middle"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'action'">
          <a-switch
            :checked="userAuth[record.id]"
            :loading="toggleLoading[record.id]"
            @change="(v: boolean) => handleToggle(record.id, v)"
          />
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'

interface OrgUser {
  id: string
  name: string
  department: string
  role: string
}

const scope = ref<'unauthorized' | 'authorized'>('unauthorized')

const mockUsers: OrgUser[] = [
  { id: 'u-001', name: '张建国', department: '工程部', role: '部长' },
  { id: 'u-002', name: '李小明', department: '人事部', role: '专员' },
  { id: 'u-003', name: '王大壮', department: '工程部', role: '工程师' },
  { id: 'u-004', name: '赵丽华', department: '财务部', role: '部长' },
  { id: 'u-005', name: '陈晓峰', department: '市场部', role: '主管' },
  { id: 'u-006', name: '林志远', department: '技术部', role: '部长' },
  { id: 'u-007', name: '刘伟', department: '安全部', role: '安全员' },
  { id: 'u-008', name: '周慧芳', department: '市场部', role: '专员' },
  { id: 'u-009', name: '孙建华', department: '安全部', role: '部长' },
  { id: 'u-010', name: '吴芳', department: '行政部', role: '专员' },
  { id: 'u-011', name: '杨红', department: '行政部', role: '主管' },
  { id: 'u-012', name: '何丽', department: '人事部', role: '专员' },
  { id: 'u-013', name: '张悦', department: '市场部', role: '专员' },
  { id: 'u-014', name: '冯杰', department: '经营部', role: '经理' },
  { id: 'u-015', name: '郑涛', department: '施工部', role: '工程师' },
  { id: 'u-016', name: '王琳', department: '经营部', role: '专员' },
  { id: 'u-017', name: '周晓', department: '技术部', role: '工程师' },
  { id: 'u-018', name: '陈晨', department: '设计部', role: '设计师' },
]

const columns = [
  { title: '姓名', dataIndex: 'name', key: 'name', width: 120 },
  { title: '部门', dataIndex: 'department', key: 'department', width: 100 },
  { title: '角色', dataIndex: 'role', key: 'role', width: 80 },
  { title: '配音权限', key: 'action', width: 100 },
]

const userAuth = ref<Record<string, boolean>>(
  Object.fromEntries(
    mockUsers.map((u) => [u.id, ['u-001', 'u-003', 'u-004', 'u-006', 'u-009', 'u-014', 'u-018'].includes(u.id)]),
  ),
)
const toggleLoading = ref<Record<string, boolean>>({})

const displayUsers = computed(() => {
  if (scope.value === 'authorized') return mockUsers.filter((u) => userAuth.value[u.id])
  return mockUsers.filter((u) => !userAuth.value[u.id])
})

function handleToggle(userId: string, checked: boolean): void {
  toggleLoading.value = { ...toggleLoading.value, [userId]: true }
  setTimeout(() => {
    userAuth.value = { ...userAuth.value, [userId]: checked }
    toggleLoading.value = { ...toggleLoading.value, [userId]: false }
    message.success(checked ? '已授权' : '已取消授权')
  }, 400)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.perm-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-md;
}
.perm-panel__count {
  font-size: @font-size-sm;
  color: @text-secondary;
}
</style>
