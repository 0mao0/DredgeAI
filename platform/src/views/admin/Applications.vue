<template>
  <div class="page-container">
    <div class="page-header">
      <h2>应用管理</h2>
      <p>管理应用分组、上下架、版本与授权范围</p>
    </div>

    <a-card class="app-mgr-card">
      <div style="margin-bottom: 16px; display: flex; justify-content: space-between">
        <a-space>
          <a-button type="primary"><PlusOutlined /> 新建应用</a-button>
          <a-button>批量上架</a-button>
          <a-button>批量下架</a-button>
        </a-space>
        <a-space>
          <a-select :value="'all'" style="width: 120px" @change="(v: string) => groupFilter = v">
            <a-select-option value="all">全部分组</a-select-option>
            <a-select-option value="日常办公">日常办公</a-select-option>
            <a-select-option value="专业业务">专业业务</a-select-option>
            <a-select-option value="知识查询">知识查询</a-select-option>
            <a-select-option value="开发接口">开发接口</a-select-option>
          </a-select>
          <a-input-search placeholder="搜索应用名称" style="width: 200px" />
        </a-space>
      </div>
      <a-table :data-source="filteredApps" :columns="columns" :pagination="{ pageSize: 10 }" row-key="id" size="middle">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === '已上架' ? 'green' : record.status === '已下架' ? 'default' : 'orange'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small">编辑</a-button>
            <a-button type="link" size="small" v-if="record.status === '已上架'">下架</a-button>
            <a-button type="link" size="small" v-else-if="record.status === '待审核'">审核</a-button>
            <a-button type="link" size="small" danger>删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { appManagementList } from '@/mock/data'

const groupFilter = ref('all')

const filteredApps = computed(() => {
  if (groupFilter.value === 'all') return appManagementList
  return appManagementList.filter(a => a.group === groupFilter.value)
})

const columns = [
  { title: '应用名称', dataIndex: 'name', key: 'name' },
  { title: '分组', dataIndex: 'group', key: 'group' },
  { title: '版本', dataIndex: 'version', key: 'version' },
  { title: '状态', dataIndex: 'status', key: 'status' },
  { title: '可见范围', dataIndex: 'scope', key: 'scope' },
  { title: '操作', key: 'action' },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.app-mgr-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
</style>
