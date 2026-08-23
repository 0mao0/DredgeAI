<template>
  <SectionCard title="会后报告" flush>
    <a-skeleton v-if="!report" :paragraph="{ rows: 6 }" />
    <template v-else>
      <div class="report-step__block">
        <div class="label">出勤</div>
        <a-table
          size="small"
          row-key="workerId"
          :columns="[
            { title: '姓名', dataIndex: 'name' },
            { title: '班组', dataIndex: 'team' },
            { title: '状态', dataIndex: 'status', width: 110 },
          ]"
          :data-source="report.attendance"
          :pagination="false"
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
import type { ReportDto } from '@/types'

defineProps<{ report: ReportDto | null }>()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.report-step__block {
  margin-bottom: @spacing-lg;
}
</style>
