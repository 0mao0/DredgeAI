<template>
  <div class="page-container">
    <PageHeader title="日志管理" description="系统操作日志与审计" />
    <SectionCard title="操作日志">
      <a-empty description="暂无日志" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
