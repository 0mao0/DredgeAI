<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理 API 密钥和调用">
      <template #extra>
        <a-button type="primary">新建密钥</a-button>
      </template>
    </PageHeader>
    <SectionCard title="API 使用说明" class="mb-24">
      <p class="api-desc">使用以下端点接入智浚 AI 平台。所有请求需要在 Header 中携带 API Key。</p>
      <a-typography>
        <a-typography-paragraph>
          <pre><code>curl -X POST https://api.dredgeai.com/v1/chat
  -H "Authorization: Bearer YOUR_API_KEY"
  -H "Content-Type: application/json"</code></pre>
        </a-typography-paragraph>
      </a-typography>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
</script>

<style scoped lang="less">
@import '@/styles/variables.less';
.mb-24 { margin-bottom: @spacing-xl; }
.api-desc {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-bottom: @spacing-md;
}
pre {
  background: @divider-color;
  padding: @spacing-lg;
  border-radius: @radius-base;
  overflow-x: auto;
  code { font-size: @font-size-sm; }
}
</style>
