<template>
  <div class="standard-chat">
    <AIChat
      title="AI 问答"
      :placeholder="placeholder"
      :context-items="contextItems"
      :transport="transport"
      scene="standard-qa"
      :session-id="sessionKey"
      library-id="default"
      @remove-context="handleRemoveContext"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AIChat } from '@angineer/aichat-ui'
import '@angineer/aichat-ui/style'
import { createStandardQaTransport } from '@/api/modules/standard'

const props = defineProps<{
  standardId: string | null
  standardName: string
}>()

const transport = createStandardQaTransport()

const removedContextIds = ref<Set<string>>(new Set())

watch(() => props.standardId, () => {
  removedContextIds.value = new Set()
})

const contextItems = computed(() =>
  props.standardId && !removedContextIds.value.has(props.standardId)
    ? [{ id: props.standardId, title: props.standardName || '当前标准' }]
    : [],
)

/** 每个标准一个会话，切换标准时保留各自对话历史 */
const sessionKey = computed(() => props.standardId || 'default')

function handleRemoveContext(id: string): void {
  removedContextIds.value = new Set([...removedContextIds.value, id])
}

const placeholder = computed(() =>
  props.standardName
    ? `针对《${props.standardName}》提问…`
    : '请先在左侧选择标准',
)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.standard-chat {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;

  /* 桥接 aichat-ui 的 CSS 变量到项目主题，跟随 light/dark */
  --bg-primary: @card-bg;
  --bg-secondary: @content-bg;
  --bg-tertiary: @surface-hover;
  --text-primary: @text-primary;
  --text-secondary: @text-secondary;
  --text-tertiary: @text-tertiary;
  --border-color: @border-color;
  --primary-color: @brand-primary;
  --chat-root-bg: @card-bg;
  --chat-user-bubble-bg: @brand-primary;
  --chat-user-bubble-text: #fff;
  --chat-assistant-bubble-bg: @surface-hover;
  --chat-assistant-bubble-text: @text-primary;
  --chat-citation-accent: @brand-primary;
  --chat-citation-bg: @surface-hover;
  --chat-citation-border: @brand-primary;
  --chat-code-bg: @content-bg;
  --chat-pre-bg: @content-bg;
  --chat-error-color: @danger;
  --chat-error-hover: @danger;
  --chat-streaming-bg: @card-bg;
  --chat-streaming-cursor: @brand-primary;
  --chat-system-bg: @surface-hover;
  --chat-system-border: @border-color;
  --chat-system-text: @text-secondary;
}
</style>
