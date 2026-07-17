<template>
  <div class="page-container">
    <div class="page-header">
      <h2>数据治理</h2>
      <p>审核用户上传数据、分类标签与处理状态</p>
    </div>

    <a-card class="data-card">
      <div style="margin-bottom: 16px; display: flex; justify-content: space-between">
        <a-space>
          <a-select :value="'all'" style="width: 120px" @change="(v: string) => statusFilter = v">
            <a-select-option value="all">全部状态</a-select-option>
            <a-select-option value="待审核">待审核</a-select-option>
            <a-select-option value="已通过">已通过</a-select-option>
            <a-select-option value="已拒绝">已拒绝</a-select-option>
          </a-select>
          <a-select :value="'all'" style="width: 120px" @change="(v: string) => categoryFilter = v">
            <a-select-option value="all">全部分类</a-select-option>
            <a-select-option value="招标文件">招标文件</a-select-option>
            <a-select-option value="合同">合同</a-select-option>
            <a-select-option value="技术文档">技术文档</a-select-option>
            <a-select-option value="会议记录">会议记录</a-select-option>
            <a-select-option value="计划报告">计划报告</a-select-option>
          </a-select>
        </a-space>
        <a-input-search placeholder="搜索文件名称" style="width: 200px" />
      </div>
      <a-table :data-source="filteredData" :columns="columns" :pagination="{ pageSize: 10 }" row-key="id" size="middle">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === '已通过' ? 'green' : record.status === '已拒绝' ? 'red' : 'orange'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" v-if="record.status === '待审核'">
              <CheckOutlined /> 通过
            </a-button>
            <a-button type="link" size="small" danger v-if="record.status === '待审核'">
              <CloseOutlined /> 拒绝
            </a-button>
            <a-button type="link" size="small">查看</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-row :gutter="24" style="margin-top: 24px">
      <a-col :span="12">
        <a-card title="分类统计" class="data-card">
          <a-table :data-source="categoryStats" :columns="catColumns" :pagination="false" size="small" />
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card title="处理概览" class="data-card">
          <a-row :gutter="16">
            <a-col :span="8" v-for="s in overviewStats" :key="s.label">
              <div style="text-align: center; padding: 16px 0">
                <div style="font-size: 28px; font-weight: 700; color: #1a2332">{{ s.value }}</div>
                <div style="font-size: 13px; color: #999; margin-top: 4px">{{ s.label }}</div>
              </div>
            </a-col>
          </a-row>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { CheckOutlined, CloseOutlined } from '@ant-design/icons-vue'
import { dataItems } from '@/mock/data'

const statusFilter = ref('all')
const categoryFilter = ref('all')

const filteredData = computed(() => {
  let result = dataItems
  if (statusFilter.value !== 'all') result = result.filter(d => d.status === statusFilter.value)
  if (categoryFilter.value !== 'all') result = result.filter(d => d.category === categoryFilter.value)
  return result
})

const columns = [
  { title: '文件名称', dataIndex: 'name', key: 'name' },
  { title: '上传者', dataIndex: 'uploader', key: 'uploader' },
  { title: '上传日期', dataIndex: 'date', key: 'date' },
  { title: '分类', dataIndex: 'category', key: 'category' },
  { title: '状态', dataIndex: 'status', key: 'status' },
  { title: '操作', key: 'action' },
]

const categoryStats = [
  { category: '招标文件', count: 12, percentage: '24%' },
  { category: '合同', count: 15, percentage: '30%' },
  { category: '技术文档', count: 10, percentage: '20%' },
  { category: '会议记录', count: 6, percentage: '12%' },
  { category: '计划报告', count: 7, percentage: '14%' },
]

const catColumns = [
  { title: '分类', dataIndex: 'category', key: 'category' },
  { title: '数量', dataIndex: 'count', key: 'count' },
  { title: '占比', dataIndex: 'percentage', key: 'percentage' },
]

const overviewStats = [
  { label: '总文件数', value: '50' },
  { label: '待审核', value: '12' },
  { label: '已通过', value: '30' },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.data-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
</style>
