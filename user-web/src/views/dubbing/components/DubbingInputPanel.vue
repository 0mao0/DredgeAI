<template>
  <div class="input-panel">
    <a-textarea
      :value="props.text"
      :maxlength="5000"
      :auto-size="{ minRows: 6, maxRows: 14 }"
      :placeholder="EXAMPLE_TEXT"
      class="input-panel__textarea"
      @input="(e: Event) => emit('update:text', (e.target as HTMLTextAreaElement).value)"
    />

    <div class="input-panel__footer-row">
      <span class="input-panel__eta">{{ estimatedDuration }}</span>

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
import { computed } from 'vue'
import { SoundOutlined } from '@ant-design/icons-vue'

const props = defineProps<{
  generating: boolean
  text: string
}>()

const emit = defineEmits<{
  generate: [text: string]
  'update:text': [value: string]
}>()

const EXAMPLE_TEXT = '各位领导，各位同事，大家下午好。今天由我来为大家汇报本项目的最新进展情况。经过全体团队成员的共同努力，项目整体进度已超过预期目标。'

// 预估时长：字数 / 3.6 / 倍速（秒），TTS 服务默认 1x 倍速
const estimatedDuration = computed(() => {
  const len = props.text.trim().length || EXAMPLE_TEXT.length
  const sec = len / 3.6
  return `${sec.toFixed(1)} 秒`
})

function handleGenerate(): void {
  const content = props.text.trim() || EXAMPLE_TEXT
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
  &__eta {
    font-size: @font-size-sm;
    color: @text-primary;
    white-space: nowrap;
  }
  &__submit {
    margin-left: auto;
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
