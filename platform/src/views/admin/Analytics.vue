<template>
  <div class="page-container">
    <div class="page-header">
      <h2>统计分析</h2>
      <p>调用趋势、应用排行与成本分析</p>
    </div>

    <a-row :gutter="24">
      <a-col :span="16">
        <a-card title="调用趋势" class="analytics-card">
          <template #extra>
            <a-radio-group :value="'month'" size="small">
              <a-radio-button value="week">周</a-radio-button>
              <a-radio-button value="month">月</a-radio-button>
              <a-radio-button value="quarter">季</a-radio-button>
            </a-radio-group>
          </template>
          <div class="chart-area">
            <div class="bar-chart">
              <div v-for="(item, idx) in trend" :key="item.month" class="bar-group">
                <div class="bar-container">
                  <div
                    class="bar bar-calls"
                    :style="{ height: (item.calls / maxCalls * 200) + 'px' }"
                    :title="'调用量: ' + item.calls"
                  >
                    <span class="bar-value">{{ item.calls }}</span>
                  </div>
                  <div
                    class="bar bar-users"
                    :style="{ height: (item.users / maxUsers * 200) + 'px' }"
                    :title="'用户数: ' + item.users"
                  >
                    <span class="bar-value">{{ item.users }}</span>
                  </div>
                </div>
                <div class="bar-label">{{ item.month }}</div>
              </div>
            </div>
            <div class="chart-legend">
              <span><span class="legend-dot" style="background: #00c9b7"></span> 调用量</span>
              <span><span class="legend-dot" style="background: #1a2332"></span> 活跃用户</span>
            </div>
          </div>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card title="模型成本占比" class="analytics-card">
          <div v-for="item in modelCost" :key="item.model" class="cost-item">
            <div class="cost-header">
              <span class="cost-name">{{ item.model }}</span>
              <span class="cost-value">¥{{ item.cost.toLocaleString() }}</span>
            </div>
            <a-progress :percent="item.share" :stroke-color="getColor(item.model)" size="small" />
          </div>
        </a-card>
      </a-col>
    </a-row>

    <a-row :gutter="24" style="margin-top: 24px">
      <a-col :span="12">
        <a-card title="应用排行" class="analytics-card">
          <a-table :data-source="appRanking" :columns="rankColumns" :pagination="false" size="small">
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'rank'">
                <span :class="index < 3 ? 'top-rank' : ''">{{ index + 1 }}</span>
              </template>
              <template v-if="column.key === 'share'">
                <a-progress :percent="record.share" size="small" :stroke-color="'#00c9b7'" :show-info="false" />
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
      <a-col :span="12">
        <a-card title="关键指标" class="analytics-card">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="总调用次数" :span="2">52,400</a-descriptions-item>
            <a-descriptions-item label="本月新增">+12.5%</a-descriptions-item>
            <a-descriptions-item label="环比增长">+8.3%</a-descriptions-item>
            <a-descriptions-item label="活跃应用">12 个</a-descriptions-item>
            <a-descriptions-item label="总成本">¥28,300</a-descriptions-item>
            <a-descriptions-item label="平均响应">1.2s</a-descriptions-item>
            <a-descriptions-item label="错误率">0.3%</a-descriptions-item>
          </a-descriptions>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { analyticsTrend as trend, appRanking, modelCost } from '@/mock/data'

const maxCalls = computed(() => Math.max(...trend.map(t => t.calls)))
const maxUsers = computed(() => Math.max(...trend.map(t => t.users)))

const rankColumns = [
  { title: '排名', key: 'rank' },
  { title: '应用名称', dataIndex: 'name', key: 'name' },
  { title: '调用次数', dataIndex: 'calls', key: 'calls' },
  { title: '占比', dataIndex: 'share', key: 'share' },
]

function getColor(model: string) {
  const colors: Record<string, string> = {
    'GPT-4o': '#00c9b7',
    'Claude 3.5 Sonnet': '#1a2332',
    '本地模型': '#8b5cf6',
    '其他': '#9ca3af',
  }
  return colors[model] || '#00c9b7'
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.analytics-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}

.chart-area {
  padding: 8px 0;
}
.bar-chart {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  height: 240px;
  padding: 0 8px;
}
.bar-group {
  display: flex;
  flex-direction: column;
  align-items: center;
  flex: 1;
}
.bar-container {
  display: flex;
  align-items: flex-end;
  gap: 4px;
  height: 220px;
}
.bar {
  width: 28px;
  border-radius: 4px 4px 0 0;
  position: relative;
  transition: height 0.3s;
  min-height: 4px;
}
.bar-calls {
  background: @accent-color;
}
.bar-users {
  background: @primary-color;
}
.bar-value {
  position: absolute;
  top: -18px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 10px;
  color: @text-secondary;
  white-space: nowrap;
}
.bar-label {
  font-size: 11px;
  color: @text-secondary;
  margin-top: 8px;
}
.chart-legend {
  display: flex;
  justify-content: center;
  gap: 24px;
  margin-top: 16px;
  font-size: 13px;
  color: @text-secondary;
}
.legend-dot {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 2px;
  margin-right: 6px;
}

.cost-item {
  margin-bottom: 20px;
}
.cost-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 6px;
}
.cost-name {
  font-size: 13px;
  color: @text-primary;
  font-weight: 500;
}
.cost-value {
  font-size: 13px;
  color: @text-secondary;
}

.top-rank {
  font-weight: 700;
  color: @accent-color;
}
</style>
