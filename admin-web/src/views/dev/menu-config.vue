<template>
  <div class="page-container">
    <PageHeader title="菜单配置" description="自定义导航菜单项" />
    <SectionCard title="菜单列表">
      <a-empty description="暂无配置" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
