<template>
  <div class="page-container">
    <PageHeader title="读标" description="AI 投标 · 读标" />
    <SectionCard>
      <div class="read-placeholder">
        <div class="read-placeholder__icon">
          <FileSearchOutlined />
        </div>
        <h3>读标模块</h3>
        <p>功能开发中，敬请期待</p>
        <p class="read-placeholder__hint">当前版本为占位页面</p>
      </div>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { FileSearchOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.read-placeholder {
  text-align: center;
  padding: 60px 0;
}
.read-placeholder__icon {
  font-size: 48px;
  color: @text-tertiary;
  margin-bottom: @spacing-base;
}
h3 {
  margin-bottom: @spacing-sm;
  font-size: @font-size-xl;
  color: @text-primary;
}
p {
  color: @text-secondary;
  margin: 0;
  font-size: @font-size-base;
}
.read-placeholder__hint {
  margin-top: @spacing-md;
  font-size: @font-size-sm;
  color: @text-tertiary;
}
</style>
