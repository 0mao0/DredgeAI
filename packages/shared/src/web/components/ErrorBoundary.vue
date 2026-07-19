<template>
  <template v-if="error">
    <a-result
      status="error"
      :title="title"
      :sub-title="errorMessage"
    >
      <template #extra>
        <a-button type="primary" @click="handleReload">刷新页面</a-button>
        <a-button @click="handleReset">重试</a-button>
      </template>
    </a-result>
  </template>
  <slot v-else />
</template>

<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'

interface Props {
  title?: string
}

withDefaults(defineProps<Props>(), {
  title: '页面出现异常',
})

const error = ref<Error | null>(null)
const errorMessage = ref('')

onErrorCaptured((err: Error) => {
  error.value = err
  errorMessage.value = err.message || String(err)
  // 阻止异常继续向上传播
  return false
})

function handleReload(): void {
  if (typeof window !== 'undefined') window.location.reload()
}

function handleReset(): void {
  error.value = null
  errorMessage.value = ''
}
</script>
