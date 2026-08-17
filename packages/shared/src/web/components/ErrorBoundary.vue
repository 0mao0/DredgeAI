<template>
  <template v-if="error">
    <a-result
      status="error"
      :title="title"
      :sub-title="errorMessage"
    >
      <template #extra>
        <AppButton variant="primary" @click="handleReload">{{ reloadLabel }}</AppButton>
        <AppButton @click="handleReset">{{ retryLabel }}</AppButton>
      </template>
    </a-result>
  </template>
  <slot v-else />
</template>

<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'
import { t } from '@shared/core/i18n'
import AppButton from './AppButton.vue'

interface Props {
  title?: string
}

withDefaults(defineProps<Props>(), {
  title: t('common.error'),
})

const error = ref<Error | null>(null)
const errorMessage = ref('')
const reloadLabel = t('common.reload')
const retryLabel = t('common.retry')

onErrorCaptured((err: Error) => {
  error.value = err
  errorMessage.value = err.message || String(err)
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
