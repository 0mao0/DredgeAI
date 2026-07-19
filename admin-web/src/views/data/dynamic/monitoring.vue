<template>
  <div class="page-container">
    <PageHeader title="监控" description="系统运行监控" />
    <SectionCard title="监控面板">
      <a-empty description="暂无监控数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>
