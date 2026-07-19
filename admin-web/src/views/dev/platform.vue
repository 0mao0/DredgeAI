<template>
  <div class="page-container">
    <PageHeader title="平台信息" description="系统配置与服务状态" />
    <SectionCard title="系统信息">
      <a-empty description="暂无信息" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>
