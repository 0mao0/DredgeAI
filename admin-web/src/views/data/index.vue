<template>
  <div class="page-container">
    <PageHeader title="数据源管理" description="连接和管理数据源">
      <template #extra>
        <a-button type="primary">新增数据源</a-button>
      </template>
    </PageHeader>
    <a-row :gutter="[16, 16]">
      <a-col :span="8" v-for="ds in dataSources" :key="ds.id">
        <a-card :title="ds.name" class="ds-card">
          <template #extra>
            <a-tag :color="ds.status === '已连接' ? 'green' : ds.status === '连接失败' ? 'red' : 'orange'">
              {{ ds.status }}
            </a-tag>
          </template>
          <p class="ds-type">类型：{{ ds.type }}</p>
          <p v-if="ds.description" class="ds-desc">{{ ds.description }}</p>
          <p v-if="ds.lastSync" class="ds-sync">最后同步：{{ ds.lastSync }}</p>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@shared/components/PageHeader.vue'
import type { DataSource } from '@/types'
import { getDataSources } from '@/api/modules/datasource'

const dataSources = ref<DataSource[]>([])

onMounted(async () => {
  dataSources.value = await getDataSources()
})
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';
.ds-card {
  :deep(.ant-card-head-title) { font-size: @font-size-base; }
}
.ds-type { font-size: @font-size-sm; color: @text-secondary; margin-bottom: @spacing-xs; }
.ds-desc { font-size: @font-size-sm; color: @text-tertiary; margin-bottom: @spacing-xs; }
.ds-sync { font-size: @font-size-xs; color: @text-tertiary; }
</style>
