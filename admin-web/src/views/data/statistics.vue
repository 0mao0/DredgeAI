<template>
  <div class="page-container">
    <PageHeader title="数据统计" description="平台数据统计分析" />
    <SectionCard title="统计概览">
      <a-empty description="暂无数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
