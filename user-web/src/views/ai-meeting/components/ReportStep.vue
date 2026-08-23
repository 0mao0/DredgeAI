<template>
  <SectionCard title="会后报告" flush>
    <a-skeleton v-if="!report" :paragraph="{ rows: 6 }" />
    <template v-else>
      <div class="report-step__block">
        <div class="label">出勤</div>
        <DataTable
          :columns="columns"
          :data-source="report.attendance"
          row-key="workerId"
          :pagination="false"
          :card="false"
        />
      </div>
      <div class="report-step__block">
        <div class="label">转写稿</div>
        <a-typography-paragraph>{{ report.transcript }}</a-typography-paragraph>
      </div>
      <div class="report-step__block">
        <div class="label">问答记录</div>
        <div v-for="qa in report.qaRecords" :key="qa.id">
          <b>问：</b>{{ qa.question }}<br>
          <b>答：</b>{{ qa.answer }}
        </div>
      </div>
    </template>
  </SectionCard>
</template>

<script setup lang="ts">
import SectionCard from '@shared/web/components/SectionCard.vue'
import { DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import type { ReportDto } from '@/types'

defineProps<{ report: ReportDto | null }>()

const columns: DataTableColumn[] = [
  { title: '姓名', dataIndex: 'name', key: 'name', width: 120, minWidth: 100, resizable: true },
  { title: '班组', dataIndex: 'team', key: 'team', width: 160, minWidth: 120, resizable: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110, minWidth: 90, resizable: true },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.report-step__block {
  margin-bottom: @spacing-lg;
}
</style>
