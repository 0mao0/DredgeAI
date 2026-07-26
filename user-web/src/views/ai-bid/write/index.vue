<template>
  <div class="module-placeholder">
    <div class="placeholder-graphic">
      <EditOutlined />
    </div>
    <div class="placeholder-body">
      <h3>AI 辅助编写投标文件</h3>
      <p>基于招标要求智能生成投标文档，支持大纲生成、内容续写、格式排版。</p>
      <span class="placeholder-hint">即将上线</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { EditOutlined } from '@ant-design/icons-vue'
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.module-placeholder {
  display: flex;
  align-items: flex-start;
  gap: @spacing-xl;
  padding: @spacing-2xl 0;
  cursor: default;
}

.placeholder-graphic {
  width: 48px;
  height: 48px;
  border-radius: @radius-lg;
  background: color-mix(in srgb, @success 10%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  color: @success;
  flex-shrink: 0;
}

.placeholder-body {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: @spacing-sm;
}

h3 { font-size: @font-size-lg; color: @text-primary; margin: 0; }
p { font-size: @font-size-sm; line-height: 1.6; margin: 0; color: @text-secondary; max-width: 480px; }

.placeholder-hint {
  font-size: @font-size-xs;
  color: @text-tertiary;
  background: @content-bg;
  padding: 2px 12px;
  border-radius: 10px;
}
</style>
