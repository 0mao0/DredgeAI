<template>
  <div class="page-container">
    <PageHeader :title="pageTitle" :description="description" />
    <SectionCard>
      <a-empty class="placeholder-empty">
        <template #description>
          <div class="placeholder-text">
            <h3>「{{ pageTitle }}」模块</h3>
            <p>功能开发中，敬请期待</p>
            <p class="placeholder-hint">该模块已由管理员在 {{ pageTitle }} 中分配给您，当前版本为占位页面</p>
          </div>
        </template>
      </a-empty>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'

const route = useRoute()
const pageTitle = computed(() => (route.meta.title as string) || '未知模块')
const description = computed(() => `来自 admin-web 分配的已授权应用 — ${pageTitle.value}`)
</script>

<style scoped lang="less">
.placeholder-empty {
  padding: 60px 0;
}
.placeholder-text {
  text-align: center;
  h3 { margin-bottom: 8px; font-size: 18px; }
  p { color: var(--color-text-secondary); margin: 0; }
  .placeholder-hint {
    margin-top: 12px;
    font-size: 12px;
    color: var(--color-text-tertiary);
  }
}
</style>