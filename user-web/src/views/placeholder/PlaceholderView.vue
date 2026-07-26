<template>
  <div class="page-container">
    <PageHeader :title="pageTitle" :description="description" />
    <SectionCard>
      <EmptyState :title="`「${pageTitle}」模块`" description="功能开发中，敬请期待" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'

const route = useRoute()
const pageTitle = computed(() => (route.meta.title as string) || '未知模块')
const description = computed(() => `来自 admin-web 分配的已授权应用 — ${pageTitle.value}`)
</script>
