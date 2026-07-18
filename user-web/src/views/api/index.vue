<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理 API Key、配额与调用统计">
      <template #extra>
        <a-button type="primary" @click="showCreateModal = true">
          <plus-outlined />
          创建 Key
        </a-button>
      </template>
    </PageHeader>

    <a-row :gutter="[24, 24]">
      <a-col :span="16">
        <SectionCard title="API Key 列表" nopad>
          <a-table
            :data-source="apiKeys"
            :columns="columns"
            :pagination="false"
            row-key="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'key'">
                <span class="key-text">{{ record.key }}</span>
                <a-button type="link" size="small" @click="copyKey(record.fullKey)">
                  <copy-outlined />
                </a-button>
              </template>
              <template v-else-if="column.key === 'status'">
                <StatusTag :status="record.status" />
              </template>
              <template v-else-if="column.key === 'usage'">
                <a-progress :percent="Math.round(record.usage / record.quota * 100)" :size="'small'" />
                <div class="usage-text">{{ record.usage }} / {{ record.quota }}</div>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-button type="link" size="small">编辑</a-button>
                <a-button type="link" size="small" danger>禁用</a-button>
              </template>
            </template>
          </a-table>
        </SectionCard>
      </a-col>

      <a-col :span="8">
        <SectionCard title="按模型用量" class="mb-16">
          <ChartContainer :option="modelPieOption" height="240px" />
        </SectionCard>

        <SectionCard title="按 Key 用量">
          <ChartContainer :option="keyBarOption" height="200px" />
        </SectionCard>
      </a-col>
    </a-row>

    <a-modal v-model:open="showCreateModal" title="创建 API Key" @ok="handleCreate">
      <a-form layout="vertical">
        <a-form-item label="Key 名称" required>
          <a-input v-model:value="newKey.name" placeholder="如：生产环境" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newKey.modelType" :options="modelOptions" placeholder="选择模型" />
        </a-form-item>
        <a-form-item label="配额">
          <a-input-number v-model:value="newKey.quota" :min="1000" :step="1000" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watchEffect, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, CopyOutlined } from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import ChartContainer from '@/components/ChartContainer.vue'
import { getApiKeyList, getModelTypes, getUsageByModel, getUsageByKey } from '@/api/modules/apikey'
import type { ApiKey, ModelType, UsageByModel, UsageByKey } from '@/types'
import { useTheme } from '@/composables/useTheme'
import { cssVarValue } from '@/composables/useCssVar'

const apiKeys = ref<ApiKey[]>([])
const modelTypes = ref<ModelType[]>([])
const usageByModel = ref<UsageByModel[]>([])
const usageByKey = ref<UsageByKey[]>([])
const showCreateModal = ref(false)

const newKey = ref({ name: '', modelType: '', quota: 10000 })

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: 'Key', key: 'key' },
  { title: '模型', dataIndex: 'modelType', key: 'modelType' },
  { title: '状态', key: 'status' },
  { title: '用量', key: 'usage' },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt' },
  { title: '操作', key: 'action' },
]

const modelOptions = computed(() => modelTypes.value.map((m) => ({ label: m.name, value: m.name })))

const { currentTheme } = useTheme()

const brandColor = ref('#0EA5E9')
const successColor = ref('#10B981')
const accentColor = ref('#06B6D4')
const warningColor = ref('#F59E0B')
const dangerColor = ref('#EF4444')
const cardBgColor = ref('#FFFFFF')

watchEffect(() => {
  currentTheme.value
  brandColor.value = cssVarValue('--color-brand')
  successColor.value = cssVarValue('--color-success')
  accentColor.value = cssVarValue('--color-accent')
  warningColor.value = cssVarValue('--color-warning')
  dangerColor.value = cssVarValue('--color-danger')
  cardBgColor.value = cssVarValue('--color-card-bg')
})

const modelPieOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
  legend: { bottom: 0, type: 'scroll' },
  series: [{
    type: 'pie',
    radius: ['40%', '70%'],
    avoidLabelOverlap: false,
    itemStyle: { borderRadius: 8, borderColor: cardBgColor.value, borderWidth: 2 },
    label: { show: false },
    emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
    data: usageByModel.value.map((u) => ({ name: u.modelName, value: u.calls })),
    color: [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value],
  }],
}))

const keyBarOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: { type: 'category', data: usageByKey.value.map((u) => u.keyName) },
  yAxis: { type: 'value' },
  series: [{
    type: 'bar',
    data: usageByKey.value.map((u) => u.calls),
    itemStyle: { color: brandColor.value, borderRadius: [4, 4, 0, 0] },
    barWidth: '40%',
  }],
}))

function copyKey(key: string): void {
  navigator.clipboard.writeText(key)
  message.success('已复制到剪贴板')
}

function handleCreate(): void {
  if (!newKey.value.name || !newKey.value.modelType) {
    message.warning('请填写完整信息')
    return
  }
  message.success('API Key 创建成功')
  showCreateModal.value = false
  newKey.value = { name: '', modelType: '', quota: 10000 }
}

onMounted(async () => {
  const [k, m, um, ub] = await Promise.all([
    getApiKeyList(), getModelTypes(), getUsageByModel(), getUsageByKey(),
  ])
  apiKeys.value = k
  modelTypes.value = m
  usageByModel.value = um
  usageByKey.value = ub
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.key-text {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: @font-size-xs;
  color: @text-primary;
  background: @content-bg;
  padding: 2px @spacing-sm;
  border-radius: @radius-sm;
}
.usage-text {
  font-size: 10px;
  color: @text-tertiary;
  margin-top: 2px;
}
</style>
