<template>
  <div class="page-container">
    <PageHeader title="数据分析" description="跨应用数据分析" />
    <SectionCard title="分析概览">
      <a-empty description="暂无数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
