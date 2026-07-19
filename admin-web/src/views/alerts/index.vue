<template>
  <div class="page-container">
    <PageHeader title="预警管理" description="API 用量预警与通知" />

    <SectionCard nopad>
      <template #title>
        <a-radio-group v-model:value="filterType" size="small" button-style="solid" class="filter-group">
          <a-radio-button value="all">全部</a-radio-button>
          <a-radio-button value="call">调用次数</a-radio-button>
          <a-radio-button value="token">Token 用量</a-radio-button>
          <a-radio-button value="quota">配额限制</a-radio-button>
          <a-radio-button value="anomaly">异常调用</a-radio-button>
        </a-radio-group>
      </template>

      <a-list :data-source="filteredAlerts" :pagination="{ pageSize: 10, size: 'small' }">
        <template #renderItem="{ item }">
          <a-list-item class="alert-item">
            <a-list-item-meta>
              <template #avatar>
                <a-tag :color="typeColorMap[item.type]">{{ item.typeLabel }}</a-tag>
              </template>
              <template #title>
                <span class="alert-content">{{ item.content }}</span>
              </template>
              <template #description>
                <span class="alert-time">{{ item.createdAt }}</span>
              </template>
            </a-list-item-meta>
            <template #extra>
              <a-tag v-if="item.status === 'pending'" color="orange">未处理</a-tag>
              <a-tag v-else-if="item.status === 'confirmed'" color="green">已确认</a-tag>
              <a-tag v-else>已忽略</a-tag>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'

const filterType = ref('all')

const typeColorMap: Record<string, string> = {
  call: 'blue',
  token: 'purple',
  anomaly: 'red',
  quota: 'orange',
}

const alerts = [
  { type: 'call', typeLabel: '调用次数', content: '用户「张三」本月调用次数已达 48,521 次，超过预警阈值', createdAt: '2026-07-19 14:32', status: 'pending' },
  { type: 'token', typeLabel: 'Token 用量', content: '用户「李四」本月 Token 用量已达 85,600,000，接近限制', createdAt: '2026-07-19 11:15', status: 'pending' },
  { type: 'call', typeLabel: '调用次数', content: '全平台今日调用量较昨日上升 23%', createdAt: '2026-07-19 09:00', status: 'confirmed' },
  { type: 'anomaly', typeLabel: '异常调用', content: 'API Key「测试环境」在 5 分钟内调用频率异常（>1000 次/分钟）', createdAt: '2026-07-18 22:45', status: 'pending' },
  { type: 'quota', typeLabel: '配额限制', content: '用户「赵六」已触达月调用次数限制（328,000 次），超额请求已被拒绝', createdAt: '2026-07-18 16:20', status: 'confirmed' },
  { type: 'token', typeLabel: 'Token 用量', content: '用户「王五」本月 Token 用量已达 29,500,000，超过预警阈值', createdAt: '2026-07-18 10:10', status: 'pending' },
  { type: 'call', typeLabel: '调用次数', content: 'GPT-4o-mini 模型今日调用量突破 10 万次', createdAt: '2026-07-17 18:30', status: 'ignored' },
  { type: 'anomaly', typeLabel: '异常调用', content: '非工作时间检测到大量调用请求，来源 IP 归属地异常', createdAt: '2026-07-17 03:12', status: 'confirmed' },
]

const filteredAlerts = computed(() => {
  if (filterType.value === 'all') return alerts
  return alerts.filter((a) => a.type === filterType.value)
})
</script>

<style scoped lang="less">
.filter-group {
  margin-bottom: 0;
}

.alert-item {
  padding: 12px 24px !important;
}

.alert-content {
  font-size: 14px;
  color: var(--color-text-primary);
}

.alert-time {
  font-size: 12px;
  color: var(--color-text-tertiary);
}
</style>
