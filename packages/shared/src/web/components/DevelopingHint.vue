<template>
  <div class="page-container">
    <PageHeader :title="title" :description="description || '此页面正在开发中，敬请期待'" />
    <SectionCard>
      <div class="developing-hint">
        <ToolOutlined class="developing-hint__icon" />
        <h3>{{ title }} — 开发中</h3>
        <p>此功能正在开发中，完成后将在此处开放使用。</p>
      </div>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ToolOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'

defineProps<{
  title: string
  description?: string
}>()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.developing-hint {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: @spacing-4xl @spacing-xl;
  text-align: center;

  &__icon {
    font-size: 48px;
    color: @text-tertiary;
    margin-bottom: @spacing-lg;
  }

  h3 {
    font-size: @font-size-lg;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    margin: 0 0 @spacing-sm;
  }

  p {
    font-size: @font-size-sm;
    color: @text-tertiary;
    margin: 0;
  }
}
</style>
