<template>
  <div class="chat-panel">
    <div v-if="!messages.length" class="chat-empty">
      <BulbOutlined />
      <span>{{ emptyText }}</span>
    </div>
    <div v-else ref="chatBox" class="chat-messages">
      <div
        v-for="(msg, i) in messages"
        :key="i"
        class="chat-msg"
        :class="`chat-msg--${msg.role}`"
      >
        <div class="chat-avatar">{{ msg.role === 'user' ? '我' : 'AI' }}</div>
        <div class="chat-bubble">{{ msg.content }}</div>
      </div>
    </div>
    <div class="chat-input">
      <a-input
        v-model:value="input"
        :placeholder="placeholder"
        :disabled="disabled || loading"
        @press-enter="handleSend"
      >
        <template #suffix>
          <SendOutlined
            class="chat-send"
            :class="{ 'chat-send--active': input.trim() }"
            @click="handleSend"
          />
        </template>
      </a-input>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import { SendOutlined, BulbOutlined } from '@ant-design/icons-vue'
import type { ChatMessage } from '@shared/core/types/chat'

const props = withDefaults(defineProps<{
  messages: ChatMessage[]
  placeholder?: string
  loading?: boolean
  disabled?: boolean
  emptyText?: string
}>(), {
  placeholder: '输入内容...',
  emptyText: '暂无消息',
})

const emit = defineEmits<{ send: [text: string] }>()

const input = ref('')
const chatBox = ref<HTMLElement>()

function handleSend(): void {
  const text = input.value.trim()
  if (!text || props.loading || props.disabled) return
  emit('send', text)
  input.value = ''
}

function scrollToBottom(): void {
  nextTick(() => {
    chatBox.value?.scrollTo({ top: chatBox.value.scrollHeight, behavior: 'smooth' })
  })
}

watch(() => props.messages.length, scrollToBottom)

defineExpose({ scrollToBottom })
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.chat-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.chat-messages {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: @spacing-md;
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
}

.chat-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-sm;
  color: @text-tertiary;
  font-size: @font-size-sm;
}

.chat-msg {
  display: flex;
  gap: 6px;
  &--user { flex-direction: row-reverse; }
}

.chat-avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: @brand-gradient;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: @font-weight-semibold;
  flex-shrink: 0;
}

.chat-bubble {
  background: @content-bg;
  padding: 6px 10px;
  border-radius: 12px 12px 12px 4px;
  font-size: @font-size-sm;
  max-width: 80%;
  line-height: 1.45;
  word-break: break-word;
  .chat-msg--user & {
    background: @brand-primary;
    color: #fff;
    border-radius: 12px 12px 4px 12px;
  }
}

.chat-input {
  padding: @spacing-md;
  border-top: 1px solid @divider-color;
  flex-shrink: 0;
}

.chat-send {
  color: @text-tertiary;
  cursor: pointer;
  font-size: 16px;
  transition: color @transition-base;
  &--active { color: @brand-primary; }
}
</style>
