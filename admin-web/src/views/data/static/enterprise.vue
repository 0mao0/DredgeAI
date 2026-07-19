<template>
  <div class="page-container">
    <PageHeader title="企业库" description="企业信息数据库" />
    <SectionCard title="企业列表">
      <a-empty description="暂无企业数据" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
</script>
