<template>
  <div class="page-container">
    <PageHeader title="任务调度" description="定时任务与后台作业管理" />
    <SectionCard title="调度任务">
      <a-empty description="暂无任务" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
