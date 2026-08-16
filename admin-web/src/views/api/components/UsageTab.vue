<template>
  <div class="stats-tab">
    <div class="dimension-bar">
      <a-segmented :value="usageDimension" :options="['模型维度', '用户维度']" @update:value="emit('update:usageDimension', $event)" />
    </div>

    <template v-if="usageDimension === '模型维度'">
      <a-row :gutter="16" class="mb-24">
        <a-col :span="12">
          <MetricCard
            title="总调用次数"
            :value="formatNumber(overviewTotalCalls)"
            suffix="次"
            icon="ThunderboltOutlined"
            :color="brandColor"
          />
        </a-col>
        <a-col :span="12">
          <MetricCard
            title="总 Token 消耗量"
            :value="formatNumber(overviewTotalTokens)"
            suffix="tokens"
            icon="DatabaseOutlined"
            :color="accentColor"
          />
        </a-col>
      </a-row>

      <SectionCard title="全平台调用趋势">
        <div class="chart-header">
          <a-radio-group :value="chartMode" size="small" @update:value="emit('update:chartMode', $event)">
            <a-radio-button value="model">按模型</a-radio-button>
            <a-radio-button value="key">按 API Key</a-radio-button>
            <a-radio-button value="total">调用次数</a-radio-button>
          </a-radio-group>
          <div class="time-range-wrap">
            <a-radio-group :value="timeRange" size="small" @update:value="emit('update:timeRange', $event)">
              <a-radio-button value="7d">近7日</a-radio-button>
              <a-radio-button value="30d">近30日</a-radio-button>
              <a-radio-button value="month">本月</a-radio-button>
              <a-radio-button value="prevMonth">上月</a-radio-button>
              <a-radio-button value="custom">自定义</a-radio-button>
            </a-radio-group>
            <a-range-picker
              v-if="timeRange === 'custom'"
              :value="customDateRange"
              size="small"
              class="custom-date-picker"
              :allow-empty="false"
              @update:value="emit('update:customDateRange', $event)"
            />
          </div>
        </div>
        <ChartContainer :option="overviewChartOption" height="320px" />
      </SectionCard>

      <a-row :gutter="24" class="mt-24">
        <a-col :span="14" class="mb-24">
          <SectionCard title="模型消耗排名">
            <ChartContainer :option="userRankingChartOption" height="340px" />
          </SectionCard>
        </a-col>
        <a-col :span="10" class="mb-24">
          <SectionCard title="模型用量占比">
            <ChartContainer :option="modelPieOption" height="340px" />
          </SectionCard>
        </a-col>
      </a-row>
    </template>

    <template v-if="usageDimension === '用户维度'">
      <SectionCard nopad>
        <div class="user-filter-bar">
          <a-input-search
            :value="userKeyword"
            placeholder="搜索姓名 / 部门"
            allow-clear
            style="width:200px"
            @update:value="emit('update:userKeyword', $event)"
          />
          <a-select :value="userDepartment" allow-clear placeholder="部门" style="width:140px" @update:value="emit('update:userDepartment', $event)">
            <a-select-option v-for="d in allDepartments" :key="d" :value="d">{{ d }}</a-select-option>
          </a-select>
          <a-select
            :value="userModel"
            mode="multiple"
            allow-clear
            placeholder="全部"
            :max-tag-count="0"
            :max-tag-placeholder="userModel.length === 0 || userModel.length === allModelNames.length ? '全部' : `已选 ${userModel.length} 项`"
            style="width:140px"
            @update:value="emit('update:userModel', $event)"
          >
            <a-select-option v-for="m in allModelNames" :key="m" :value="m">{{ m }}</a-select-option>
          </a-select>
        </div>
        <a-table
          size="small"
          :data-source="mergedUserData"
          :columns="consumptionColumns"
          :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 人` }"
          row-key="userId"
          :locale="{ emptyText: '暂无数据' }"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'rank'">
              <span class="rank-badge" :class="[{ gold: index < 3 }]">{{ index + 1 }}</span>
            </template>
            <template v-else-if="column.key === 'calls'">
              {{ formatNumber(record.calls) }}
            </template>
            <template v-else-if="column.key === 'tokens'">
              {{ formatNumber(record.tokens) }}
            </template>
            <template v-else-if="column.key === 'models'">
              <a-tag :color="(record.modelLimits?.length ?? 0) === allModelNames.length ? 'green' : 'orange'">{{ (record.modelLimits?.length ?? 0) === allModelNames.length ? '全部' : '部分' }}</a-tag>
            </template>
          </template>
        </a-table>
      </SectionCard>
    </template>
  </div>
</template>

<script setup lang="ts">
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import { formatNumber } from '@shared/core/utils/format'
import { useCssVar } from '@shared/web/composables/useCssVar'
import type { MergedUserRecord, DayjsLike } from '../types'

defineProps<{
  usageDimension: string
  overviewTotalCalls: number
  overviewTotalTokens: number
  overviewChartOption: Record<string, unknown>
  userRankingChartOption: Record<string, unknown>
  modelPieOption: Record<string, unknown>
  chartMode: 'model' | 'key' | 'total'
  timeRange: string
  customDateRange: [DayjsLike, DayjsLike] | undefined
  userKeyword: string
  userDepartment: string
  userModel: string[]
  allDepartments: string[]
  allModelNames: string[]
  mergedUserData: MergedUserRecord[]
}>()

const emit = defineEmits<{
  'update:usageDimension': [value: string]
  'update:chartMode': [value: 'model' | 'key' | 'total']
  'update:timeRange': [value: string]
  'update:customDateRange': [value: [DayjsLike, DayjsLike] | undefined]
  'update:userKeyword': [value: string]
  'update:userDepartment': [value: string]
  'update:userModel': [value: string[]]
}>()

const brandColor = useCssVar('--color-brand')
const accentColor = useCssVar('--color-accent')

const consumptionColumns = [
  { title: '排名', key: 'rank', width: 70 },
  { title: '用户', dataIndex: 'name', key: 'name' },
  { title: '部门', dataIndex: 'department', key: 'department' },
  { title: '总调用次数', key: 'calls', sorter: (a: MergedUserRecord, b: MergedUserRecord) => a.calls - b.calls, sortDirections: ['ascend', 'descend'] as const },
  { title: '总 Token 用量', key: 'tokens', sorter: (a: MergedUserRecord, b: MergedUserRecord) => a.tokens - b.tokens, sortDirections: ['ascend', 'descend'] as const },
  { title: '授权模型', key: 'models', width: 100 },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.mb-24 { margin-bottom: @spacing-xl; }
.mt-24 { margin-top: @spacing-xl; }

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-lg;
}

.time-range-wrap {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}
.custom-date-picker {
  min-width: 200px;
}

.dimension-bar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: @spacing-md;
}

.stats-tab :deep(.section-card-header) {
  padding: @spacing-md @spacing-xl;
}
.stats-tab :deep(.section-card-body) {
  padding: @spacing-md @spacing-xl;
}

.user-filter-bar {
  display: flex;
  gap: @spacing-sm;
  align-items: center;
  flex-wrap: wrap;
  padding: 0;
  margin-bottom: @spacing-base;

  :deep(.ant-input-group-wrapper) {
    display: inline-flex;
    align-items: center;
    vertical-align: middle;
  }
  :deep(.ant-input-search-button) {
    display: inline-flex;
    align-items: center;
    justify-content: center;
  }
}

.rank-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: @font-size-xs;
  font-weight: @font-weight-semibold;
  background: @content-bg;
  color: @text-secondary;
  &.gold {
    background: @brand-gradient;
    color: #fff;
  }
}
</style>
