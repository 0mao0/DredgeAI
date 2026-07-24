<template>
  <div class="module-placeholder" @click="$router.push('/ai-bid/read')">
    <div class="placeholder-graphic">
      <swap-outlined />
    </div>
    <h3>多标段对比分析</h3>
    <p>同时对比多份标书的关键指标、报价、技术方案，辅助决策。</p>
    <span class="placeholder-hint">即将上线</span>
  </div>
</template>

<script setup lang="ts">
import { SwapOutlined } from '@ant-design/icons-vue'
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.module-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 400px;
  text-align: center;
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px dashed @border-color;
  padding: @spacing-2xl;
  cursor: default;
  transition: all @transition-base;
  &:hover { border-color: @border-color; box-shadow: @shadow-sm; }
}

.placeholder-graphic {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: color-mix(in srgb, @warning 10%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  color: @warning;
  margin-bottom: @spacing-lg;
}

h3 { font-size: @font-size-lg; color: @text-primary; margin: 0 0 @spacing-sm; }
p { font-size: @font-size-sm; max-width: 360px; line-height: 1.6; margin: 0 0 @spacing-lg; color: @text-secondary; }

.placeholder-hint {
  font-size: @font-size-xs;
  color: @text-tertiary;
  background: @content-bg;
  padding: 2px 12px;
  border-radius: 10px;
}
</style>
