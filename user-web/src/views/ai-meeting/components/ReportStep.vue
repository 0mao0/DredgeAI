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
        <div class="report-step__transcript">{{ report.transcript || '（暂无转写内容）' }}</div>
      </div>
      <div class="report-step__block">
        <div class="label">问答记录</div>
        <div v-if="report.qaRecords.length === 0" class="report-step__empty">（无问答记录）</div>
        <div v-for="qa in report.qaRecords" :key="qa.id" class="report-step__qa">
          <div class="report-step__qa-q"><b>问</b>{{ qa.question }}</div>
          <div class="report-step__qa-a"><b>答</b>{{ qa.answer }}</div>
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
.report-step__transcript {
  max-height: 320px;
  overflow-y: auto;
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-md;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.7;
  font-size: @font-size-sm;
}
.report-step__qa {
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-base;
  margin-bottom: @spacing-sm;
}
.report-step__qa-q {
  color: @text-primary;
  margin-bottom: @spacing-xs;

  b {
    margin-right: @spacing-xs;
    color: @brand-primary;
  }
}
.report-step__qa-a {
  color: @text-secondary;
  line-height: 1.6;

  b {
    margin-right: @spacing-xs;
    color: @text-tertiary;
  }
}
.report-step__empty {
  color: @text-tertiary;
  font-size: @font-size-sm;
}
</style>
