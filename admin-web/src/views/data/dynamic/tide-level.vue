<template>
  <div class="page-container">
    <PageHeader title="潮位" description="潮位数据监测" />
    <SectionCard title="潮位数据">
      <a-empty description="暂无数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>
