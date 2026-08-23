<template>
  <SectionCard title="晨会稿" flush>
    <AppButton v-if="!draft" variant="primary" block :loading="loading" @click="emit('generate')">
      {{ loading ? '正在生成晨会稿…' : '生成晨会稿' }}
    </AppButton>
    <template v-else>
      <a-textarea v-model:value="content" :disabled="!editing" :rows="10" />
      <div class="speech-draft-step__actions">
        <AppButton size="sm" @click="editing = !editing">
          {{ editing ? '取消编辑' : '编辑' }}
        </AppButton>
        <AppButton size="sm" :disabled="!editing" @click="onSave">保存</AppButton>
        <AppButton
          size="sm"
          :loading="audioLoading"
          @click="playing ? emit('stopAudio') : emit('playAudio')"
        >
          {{ playing ? '停止播放' : '播放晨会稿' }}
        </AppButton>
      </div>
      <AppButton variant="primary" size="lg" block :loading="loading" @click="emit('confirm')">
        确认并开始点名
      </AppButton>
    </template>
  </SectionCard>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { SpeechDraftDto } from '@/types'

const props = defineProps<{
  draft: SpeechDraftDto | null
  loading: boolean
  playing: boolean
  audioLoading: boolean
}>()
const emit = defineEmits<{
  generate: []
  save: [content: string]
  confirm: []
  playAudio: []
  stopAudio: []
}>()

const editing = ref(false)
const content = ref('')

watch(
  () => props.draft,
  (d) => {
    if (d) content.value = d.content
  },
  { immediate: true },
)

function onSave(): void {
  emit('save', content.value)
  editing.value = false
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.speech-draft-step__actions {
  display: flex;
  gap: @spacing-sm;
  margin-top: @spacing-md;
}
</style>
