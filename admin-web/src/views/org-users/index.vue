<template>
  <div class="page-container">
    <PageHeader title="组织用户" description="用户与组织结构管理" />
    <SectionCard title="用户列表">
      <a-empty description="暂无用户" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>
