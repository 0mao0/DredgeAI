<template>
  <div class="page-container">
    <div class="page-header">
      <h2>权限管理</h2>
      <p>管理角色、菜单、应用与数据权限</p>
    </div>

    <a-card class="perm-card">
      <div style="margin-bottom: 16px; display: flex; justify-content: space-between">
        <a-button type="primary"><PlusOutlined /> 新增角色</a-button>
      </div>
      <a-table :data-source="permissions" :columns="columns" :pagination="false" size="middle" row-key="id">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small">编辑</a-button>
            <a-button type="link" size="small" danger>删除</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-row :gutter="24" style="margin-top: 24px">
      <a-col :span="12">
        <a-card title="菜单权限配置" class="perm-card">
          <a-tree
            :tree-data="menuTree"
            checkable
            default-expand-all
            :checked-keys="['user-dashboard', 'user-apps', 'user-bid']"
          />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card title="数据权限范围" class="perm-card">
          <a-radio-group :value="'self'" style="display: flex; flex-direction: column; gap: 12px">
            <a-radio value="all">全部数据</a-radio>
            <a-radio value="department">本部门数据</a-radio>
            <a-radio value="self">本人数据</a-radio>
            <a-radio value="none">无权限</a-radio>
          </a-radio-group>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { PlusOutlined } from '@ant-design/icons-vue'
import { permissions } from '@/mock/data'

const columns = [
  { title: '角色名称', dataIndex: 'role', key: 'role' },
  { title: '菜单权限', dataIndex: 'menu', key: 'menu' },
  { title: '应用权限', dataIndex: 'app', key: 'app' },
  { title: '数据权限', dataIndex: 'data', key: 'data' },
  { title: '操作', key: 'action' },
]

const menuTree = [
  {
    title: 'userWeb',
    key: 'user',
    children: [
      { title: '工作台', key: 'user-dashboard' },
      { title: '应用广场', key: 'user-apps' },
      { title: 'AI 审标', key: 'user-bid' },
      { title: '标准查询', key: 'user-standards' },
      { title: '个人中心', key: 'user-profile' },
      { title: 'API 管理', key: 'user-api' },
    ],
  },
  {
    title: 'adminWeb',
    key: 'admin',
    children: [
      { title: '管理工作台', key: 'admin-dashboard' },
      { title: '权限管理', key: 'admin-permissions' },
      { title: '应用管理', key: 'admin-apps' },
      { title: '数据治理', key: 'admin-data' },
      { title: '统计分析', key: 'admin-analytics' },
    ],
  },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.perm-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
</style>
