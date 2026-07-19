<template>
  <div class="page-container">
    <PageHeader title="报告库" description="报告与文档数据库" />
    <SectionCard title="报告列表">
      <a-empty description="暂无报告数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>
