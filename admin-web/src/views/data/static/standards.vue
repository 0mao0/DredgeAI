<template>
  <div class="page-container">
    <PageHeader title="标准库" description="行业标准数据库" />
    <SectionCard title="标准列表">
      <a-empty description="暂无标准数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
