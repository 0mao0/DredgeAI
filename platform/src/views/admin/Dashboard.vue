<template>
  <div class="page-container">
    <div class="page-header">
      <h2>管理工作台</h2>
      <p>关键指标、待办告警与平台概览</p>
    </div>

    <a-row :gutter="[24, 24]">
      <a-col v-for="m in store.metrics" :key="m.title" :span="6">
        <a-card class="metric-card" :body-style="{ padding: '20px 24px' }">
          <div class="metric-title">{{ m.title }}</div>
          <div class="metric-value">{{ m.value }}</div>
          <div class="metric-trend" :class="m.trendUp ? 'trend-up' : 'trend-down'">
            {{ m.trend }} <CaretUpOutlined v-if="m.trendUp" /><CaretDownOutlined v-else />
          </div>
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="24" style="margin-top: 24px">
      <a-col :span="12">
        <a-card title="待办事项" class="admin-card">
          <a-list size="small" :data-source="todos">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta :title="item.title" :description="item.time" />
                <a-tag :color="item.level === '紧急' ? 'red' : 'orange'">{{ item.level }}</a-tag>
              </a-list-item>
            </template>
          </a-list>
        </a-card>
        <a-card title="审核提醒" class="admin-card" style="margin-top: 16px">
          <a-list size="small" :data-source="reviews">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta :title="item.title" :description="item.uploader + ' · ' + item.time" />
                <a-button type="link" size="small">审核</a-button>
              </a-list-item>
            </template>
          </a-list>
        </a-card>
      </a-col>

      <a-col :span="12">
        <a-card title="应用调用排行" class="admin-card">
          <a-table :data-source="appRanking" :columns="rankColumns" :pagination="false" size="small">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'share'">
                <a-progress :percent="record.share" size="small" :stroke-color="'#00c9b7'" :show-info="false" />
                <span style="margin-left: 8px; font-size: 12px">{{ record.share }}%</span>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { useAppStore } from '@/stores/app'
import { appRanking } from '@/mock/data'
import { CaretUpOutlined, CaretDownOutlined } from '@ant-design/icons-vue'

const store = useAppStore()

const todos = [
  { title: '新应用 "文档比对" 上架审核', time: '2小时前', level: '紧急' },
  { title: '用户权限变更申请（3条）', time: '4小时前', level: '紧急' },
  { title: '数据治理：5条内容待审核', time: '1天前', level: '普通' },
  { title: '模型调用量异常增长告警', time: '1天前', level: '普通' },
]

const reviews = [
  { title: 'XX项目招标文件.pdf', uploader: '张明', time: '2026-07-17' },
  { title: '合同模板_v3.docx', uploader: '李华', time: '2026-07-16' },
  { title: '会议纪要_0714.pdf', uploader: '赵磊', time: '2026-07-14' },
]

const rankColumns = [
  { title: '应用名称', dataIndex: 'name', key: 'name' },
  { title: '调用次数', dataIndex: 'calls', key: 'calls' },
  { title: '占比', dataIndex: 'share', key: 'share' },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.metric-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
.metric-title {
  font-size: 13px;
  color: @text-secondary;
  margin-bottom: 8px;
}
.metric-value {
  font-size: 28px;
  font-weight: 700;
  color: @text-primary;
}
.metric-trend {
  font-size: 13px;
  margin-top: 4px;
}

.admin-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
</style>
