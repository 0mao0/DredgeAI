<template>
  <div class="page-container">
    <PageHeader title="经验库" description="经验知识库" />
    <SectionCard title="经验列表">
      <a-empty description="暂无经验数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>
