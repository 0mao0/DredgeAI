<template>
  <div class="input-panel">
    <a-textarea
      v-model:value="text"
      :maxlength="5000"
      :auto-size="{ minRows: 6, maxRows: 14 }"
      :placeholder="EXAMPLE_TEXT"
      class="input-panel__textarea"
    />

    <div class="input-panel__footer-row">
      <div class="input-panel__speed-col">
        <div class="input-panel__speed-head">
          <span class="input-panel__speed-label">倍速</span>
          <span class="input-panel__speed-val">{{ speed.toFixed(1) }}x</span>
        </div>
        <a-slider
          :min="0.5"
          :max="3"
          :step="0.1"
          :value="speed"
          @update:value="(val: number) => emit('update:speed', val)"
          class="input-panel__slider"
        />
      </div>

      <div class="input-panel__eta-col">
        <span class="input-panel__eta-label">预估时长</span>
        <span class="input-panel__eta-value">{{ estimatedDuration }}</span>
      </div>

      <a-button
        type="primary"
        size="large"
        class="input-panel__submit"
        :class="{ 'is-generating': generating }"
        :loading="generating"
        :disabled="generating"
        @click="handleGenerate"
      >
        <template #icon><SoundOutlined v-if="!generating" /></template>
        {{ generating ? '生成中…' : '开始' }}
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { SoundOutlined } from '@ant-design/icons-vue'

const props = defineProps<{
  speed: number
  generating: boolean
}>()

const emit = defineEmits<{
  generate: [text: string]
  'update:speed': [value: number]
}>()

const text = ref('')

const EXAMPLE_TEXT = '各位领导，各位同事，大家下午好。今天由我来为大家汇报本项目的最新进展情况。经过全体团队成员的共同努力，项目整体进度已超过预期目标。'

// 预估时长：字数 / 3.6 / 倍速（秒）
const estimatedDuration = computed(() => {
  const len = text.value.trim().length || EXAMPLE_TEXT.length
  const sec = len / 3.6 / (props.speed || 1)
  return `${sec.toFixed(1)} 秒`
})

function handleGenerate(): void {
  const content = text.value.trim() || EXAMPLE_TEXT
  emit('generate', content)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.input-panel {
  height: 100%;
  display: flex;
  flex-direction: column;

  &__textarea { margin-bottom: @spacing-sm; flex: 1; min-height: 120px; }

  &__footer-row {
    display: flex;
    align-items: center;
    gap: @spacing-md;
    margin-top: @spacing-sm;
  }
  &__speed-col {
    flex: 1 1 auto;
    min-width: 0;
    display: flex;
    flex-direction: column;
    justify-content: center;
  }
  &__speed-head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    margin-bottom: 2px;
  }
  &__speed-label { font-size: @font-size-sm; color: @text-primary; white-space: nowrap; }
  &__speed-val {
    font-size: @font-size-sm;
    color: @text-secondary;
    font-variant-numeric: tabular-nums;
  }
  &__slider { width: 100%; :deep(.ant-slider) { margin: 0; } }
  &__eta-col {
    flex: 0 0 16%;
    max-width: 16%;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2px;
    text-align: center;
  }
  &__eta-label { font-size: @font-size-xs; color: @text-tertiary; white-space: nowrap; }
  &__eta-value {
    font-size: @font-size-base;
    color: @text-primary;
    font-weight: @font-weight-medium;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
  &__submit {
    flex: 0 0 20%;
    max-width: 20%;
    height: 44px;
    font-size: @font-size-base;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    transition: transform 0.12s ease, box-shadow 0.2s ease;
    &:active:not(:disabled) { transform: scale(0.98); }
    &.is-generating { opacity: 0.9; }
  }
}

@media (prefers-reduced-motion: reduce) {
  .input-panel__submit { transition: box-shadow 0.2s ease; &:active:not(:disabled) { transform: none; } }
}
</style>
