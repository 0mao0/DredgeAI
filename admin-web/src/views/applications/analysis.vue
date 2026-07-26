<template>
  <div class="page-container">
    <PageHeader title="数据分析" description="跨应用数据分析" />
    <SectionCard title="分析概览">
      <EmptyState />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
</script>
