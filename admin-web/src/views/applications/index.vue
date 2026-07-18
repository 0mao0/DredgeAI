<template>
  <div class="page-container">
    <PageHeader title="应用管理" description="管理所有 AI 应用">
      <template #extra>
        <a-button type="primary">新增应用</a-button>
      </template>
    </PageHeader>
    <SectionCard title="应用列表">
      <a-table
        :data-source="applications"
        :columns="columns"
        :pagination="{ pageSize: 10 }"
        :loading="loading"
        row-key="id"
        size="middle"
      />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import type { ApplicationItem } from '@/types'
import { getApplications } from '@/api/modules/applications'

const loading = ref(false)
const applications = ref<ApplicationItem[]>([])

const columns = [
  { title: '应用名称', dataIndex: 'name', key: 'name' },
  { title: '分类', dataIndex: 'category', key: 'category', width: 100 },
  { title: '负责人', dataIndex: 'manager', key: 'manager', width: 80 },
  { title: '版本', dataIndex: 'version', key: 'version', width: 80 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80 },
  { title: '用户数', dataIndex: 'userCount', key: 'userCount', width: 80 },
  { title: 'API 调用', dataIndex: 'apiCalls', key: 'apiCalls', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 100 },
]

onMounted(async () => {
  loading.value = true
  applications.value = await getApplications()
  loading.value = false
})
</script>
