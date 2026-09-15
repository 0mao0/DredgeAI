<template>
  <DataTable
    :columns="columns"
    :data-source="clusters"
    row-key="id"
    :loading="loading"
    storage-key="admin-gateway-clusters"
  >
    <template #toolbarExtra>
      <AppButton v-if="can('createCluster')" variant="primary" size="sm" @click="emit('create')">
        新增集群
      </AppButton>
    </template>
    <template #bodyCell="{ column, record }">
      <template v-if="column.key === 'destinations'">
        <div class="destinations-cell">
          <div v-for="[name, dest] in destinationsOf(record)" :key="name" class="destinations-cell__row">
            <span class="destinations-cell__name">{{ name }}</span>
            <span class="destinations-cell__arrow">→</span>
            <span class="destinations-cell__address">{{ dest.address }}</span>
          </div>
        </div>
      </template>
      <template v-else-if="column.key === 'routeCount'">
        <AppButton
          v-if="(routeCounts[record.clusterId] ?? 0) > 0"
          variant="link"
          size="sm"
          @click="emit('viewRoutes', record.clusterId)"
        >
          {{ routeCounts[record.clusterId] }}
        </AppButton>
        <span v-else>0</span>
      </template>
      <template v-else-if="column.key === 'isEnabled'">
        <a-tag :color="record.isEnabled ? 'green' : 'red'">{{ record.isEnabled ? '启用' : '禁用' }}</a-tag>
      </template>
      <template v-else-if="column.key === 'action'">
        <div class="clusters-tab__actions">
          <AppButton v-if="can('updateCluster')" variant="link" size="sm" @click="emit('edit', record)">编辑</AppButton>
          <template v-if="can('deleteCluster')">
            <a-tooltip
              v-if="(routeCounts[record.clusterId] ?? 0) > 0"
              :title="`存在 ${routeCounts[record.clusterId]} 条引用路由，无法删除`"
            >
              <AppButton variant="link" size="sm" danger disabled>删除</AppButton>
            </a-tooltip>
            <a-popconfirm
              v-else
              title="确认删除该集群？保存后即时生效"
              placement="left"
              @confirm="emit('remove', record)"
            >
              <AppButton variant="link" size="sm" danger>删除</AppButton>
            </a-popconfirm>
          </template>
        </div>
      </template>
    </template>
  </DataTable>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import type { ClusterDestination, ProxyClusterItem } from '@/api/modules/gateway'

const props = defineProps<{
  clusters: ProxyClusterItem[]
  loading: boolean
  routeCounts: Record<string, number>
  can: (action: string) => boolean
}>()

const emit = defineEmits<{
  create: []
  edit: [record: ProxyClusterItem]
  remove: [record: ProxyClusterItem]
  viewRoutes: [clusterId: string]
}>()

// DataTable 插槽 record 为宽松类型，此处收敛回强类型
function destinationsOf(record: ProxyClusterItem): Array<[string, ClusterDestination]> {
  return Object.entries(record.destinations ?? {})
}

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '集群 ID', dataIndex: 'clusterId', key: 'clusterId', width: 200, minWidth: 140, resizable: true },
    { title: '描述', dataIndex: 'description', key: 'description', width: 220, minWidth: 140, resizable: true, flex: true },
    { title: '目标节点', key: 'destinations', width: 320, minWidth: 200, resizable: true, flex: true },
    { title: '关联路由数', key: 'routeCount', width: 110, minWidth: 90 },
    { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 80, minWidth: 70 },
    { title: '操作', key: 'action', width: 180, minWidth: 120, fixed: 'right' },
  ]
  return props.can('updateCluster') || props.can('deleteCluster') ? base : base.filter((c) => c.key !== 'action')
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.destinations-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;

  &__row {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: @font-size-xs;
  }

  &__name {
    color: @text-secondary;
  }

  &__arrow {
    color: @text-tertiary;
  }

  &__address {
    word-break: break-all;
  }
}

.clusters-tab__actions {
  display: flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}
</style>
