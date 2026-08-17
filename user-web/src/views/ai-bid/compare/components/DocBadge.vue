<template>
  <span class="doc-badge">{{ label }}</span>
</template>

<script setup lang="ts">
defineProps<{
  label: string
}>()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.doc-badge {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  min-width: 22px;
  height: 24px;
  padding: 0 5px 0 8px;
  border: 1px solid @text-tertiary;
  border-radius: 2px 5px 5px 2px;
  font-size: @font-size-xs;
  font-weight: @font-weight-semibold;
  line-height: 1;
  color: @text-secondary;
  background: transparent;
  user-select: none;

  &::before {
    content: '';
    position: absolute;
    left: 4px;
    top: 3px;
    bottom: 3px;
    width: 1px;
    background: @text-tertiary;
  }
}
</style>
