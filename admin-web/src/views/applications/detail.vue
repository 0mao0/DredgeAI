<template>
  <div class="page-container">
    <a-button class="back-btn" type="link" size="small" @click="router.back()">
      <ArrowLeftOutlined /> 返回
    </a-button>
    <PageHeader :title="appName" description="应用详情与配置" />
    <SectionCard title="基本信息">
      <EmptyState title="暂无信息" />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeftOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { ApplicationItem } from '@/types'
import { getApplications } from '@/api/modules/applications'

const route = useRoute()
const router = useRouter()
const appName = ref('应用详情')

onMounted(async () => {
  try {
    const apps = await getApplications()
    const slug = `/applications/${route.params.id}`
    const app = apps.find((a: ApplicationItem) => a.id === route.params.id || a.route === slug)
    if (app) appName.value = app.name
  } catch {
    // fallback
  }
})
</script>

<style scoped lang="less">
.back-btn {
  margin-bottom: 0;
  padding-left: 0;
}
</style>
