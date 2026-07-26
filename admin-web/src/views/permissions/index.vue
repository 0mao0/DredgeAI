<template>
  <div class="page-container">
    <PageHeader title="权限管理" description="管理系统角色和权限">
      <template #extra>
        <a-button type="primary">新增权限</a-button>
      </template>
    </PageHeader>
    <SectionCard title="权限列表">
      <a-table
        :data-source="permissions"
        :columns="columns"
        :pagination="{ pageSize: 10 }"
        :loading="loading"
        row-key="id"
        size="small"
      />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { PermissionItem } from '@/types'
import { getPermissions } from '@/api/modules/permissions'

const loading = ref(false)
const permissions = ref<PermissionItem[]>([])

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: '权限编码', dataIndex: 'code', key: 'code' },
  { title: '类型', dataIndex: 'type', key: 'type', width: 80 },
  { title: '排序', dataIndex: 'sort', key: 'sort', width: 60 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80 },
]

onMounted(async () => {
  loading.value = true
  permissions.value = await getPermissions()
  loading.value = false
})
</script>
