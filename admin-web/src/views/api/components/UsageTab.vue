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
      <DataTable
        v-model:query="query"
        :columns="columns"
        :data-source="mergedUserData"
        :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 人` }"
        :filters="filters"
        row-key="userId"
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
      </DataTable>
    </template>
  </div>
</template>

<script setup lang="ts">
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import { DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { formatNumber } from '@shared/core/utils/format'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { computed } from 'vue'
import type { MergedUserRecord, DayjsLike } from '../types'

const props = defineProps<{
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

const filters: DataTableFilter[] = [
  { key: 'userKeyword', type: 'input', placeholder: '搜索姓名 / 部门', width: 200 },
  { key: 'userDepartment', type: 'select', placeholder: '部门', width: 140, options: props.allDepartments },
  { key: 'userModel', type: 'select', multiple: true, placeholder: '全部', width: 160, options: props.allModelNames },
]

const query = computed({
  get: () => ({ userKeyword: props.userKeyword, userDepartment: props.userDepartment, userModel: props.userModel }),
  set: (v: { userKeyword: string, userDepartment: string, userModel: string[] }) => {
    if (v.userKeyword !== props.userKeyword) emit('update:userKeyword', v.userKeyword)
    if (v.userDepartment !== props.userDepartment) emit('update:userDepartment', v.userDepartment)
    if (v.userModel !== props.userModel) emit('update:userModel', v.userModel)
  },
})

const columns: DataTableColumn[] = [
  { title: '排名', key: 'rank', width: 70, minWidth: 60, resizable: true },
  { title: '用户', dataIndex: 'name', key: 'name', width: 120, minWidth: 100, resizable: true },
  { title: '部门', dataIndex: 'department', key: 'department', width: 120, minWidth: 100, resizable: true },
  { title: '总调用次数', key: 'calls', width: 130, minWidth: 110, sorter: (a: MergedUserRecord, b: MergedUserRecord) => a.calls - b.calls, sortDirections: ['ascend', 'descend'] as const, resizable: true },
  { title: '总 Token 用量', key: 'tokens', width: 140, minWidth: 120, sorter: (a: MergedUserRecord, b: MergedUserRecord) => a.tokens - b.tokens, sortDirections: ['ascend', 'descend'] as const, resizable: true },
  { title: '授权模型', key: 'models', width: 100, minWidth: 90, resizable: true },
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
