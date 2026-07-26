<template>
  <div class="page-container">
    <PageHeader :title="pageTitle" :description="description" />
    <SectionCard>
      <div class="placeholder-content">
        <div class="placeholder-icon">
          <ToolOutlined />
        </div>
        <h3>「{{ pageTitle }}」模块</h3>
        <p>功能开发中，敬请期待</p>
        <p class="placeholder-hint">该模块已由管理员在 {{ pageTitle }} 中分配给您，当前版本为占位页面</p>
      </div>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { ToolOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'

const route = useRoute()
const pageTitle = computed(() => (route.meta.title as string) || '未知模块')
const description = computed(() => `来自 admin-web 分配的已授权应用 — ${pageTitle.value}`)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.placeholder-content {
  text-align: center;
  padding: 60px 0;
}
.placeholder-icon {
  font-size: 48px;
  color: @text-tertiary;
  margin-bottom: @spacing-base;
}
h3 { margin-bottom: @spacing-sm; font-size: @font-size-xl; color: @text-primary; }
p { color: @text-secondary; margin: 0; font-size: @font-size-base; }
.placeholder-hint {
  margin-top: @spacing-md;
  font-size: @font-size-sm;
  color: @text-tertiary;
}
</style>
