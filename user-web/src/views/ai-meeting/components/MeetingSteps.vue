<template>
  <div class="meeting-steps">
    <div
      v-for="(item, index) in items"
      :key="index"
      class="meeting-steps__item"
      :class="{
        'is-active': index === current,
        'is-done': index < current,
        'is-clickable': index < current,
      }"
      @click="onClick(index)"
    >
      <div class="meeting-steps__dot">
        <CheckOutlined v-if="index < current" />
        <span v-else>{{ index + 1 }}</span>
      </div>
      <div class="meeting-steps__label">{{ item }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { CheckOutlined } from '@ant-design/icons-vue'

defineProps<{
  items: string[]
  current: number
}>()
const emit = defineEmits<{
  go: [index: number]
}>()

function onClick(index: number): void {
  // 只允许回退到已完成步骤
  emit('go', index)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.meeting-steps {
  display: flex;
  align-items: flex-start;
  margin-bottom: @spacing-lg;
}
.meeting-steps__item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-xs;
  position: relative;
  min-width: 0;

  &::before {
    content: '';
    position: absolute;
    top: 11px;
    left: -50%;
    width: 100%;
    height: 2px;
    background: @border-color;
    z-index: 0;
  }
  &:first-child::before {
    display: none;
  }
  &.is-done::before,
  &.is-active::before {
    background: @brand-primary;
  }
  &.is-clickable {
    cursor: pointer;
  }
}
.meeting-steps__dot {
  position: relative;
  z-index: 1;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: @font-size-xs;
  color: @text-tertiary;
  background: @bg-elevated;
  border: 1px solid @border-color;

  .is-done & {
    background: @brand-primary;
    border-color: @brand-primary;
    color: #fff;
  }
  .is-active & {
    border-color: @brand-primary;
    color: @brand-primary;
    font-weight: @font-weight-semibold;
  }
}
.meeting-steps__label {
  font-size: 11px;
  color: @text-tertiary;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;

  .is-active & {
    color: @brand-primary;
    font-weight: @font-weight-semibold;
  }
  .is-done & {
    color: @text-secondary;
  }
}
</style>
