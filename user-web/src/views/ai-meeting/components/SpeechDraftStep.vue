<template>
  <SectionCard title="晨会稿" flush>
    <div v-if="!draft" class="speech-draft-step__generate">
      <div v-if="loading" class="speech-draft-step__loading">
        <div class="speech-draft-step__spinner">
          <span class="speech-draft-step__dot" />
          <span class="speech-draft-step__dot" />
          <span class="speech-draft-step__dot" />
        </div>
        <div class="speech-draft-step__loading-title">AI生成晨会稿中...</div>
        <div class="speech-draft-step__loading-sub">正在结合今日计划与知识库组织语言，请稍候</div>
      </div>
      <AppButton v-else variant="primary" block @click="emit('generate')">生成晨会稿</AppButton>
    </div>
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
.speech-draft-step__loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-md;
  padding: @spacing-2xl 0;
}
.speech-draft-step__spinner {
  display: flex;
  gap: @spacing-sm;
}
.speech-draft-step__dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: @brand-primary;
  animation: speech-draft-bounce 1.2s infinite ease-in-out;

  &:nth-child(2) {
    animation-delay: 0.15s;
  }
  &:nth-child(3) {
    animation-delay: 0.3s;
  }
}
.speech-draft-step__loading-title {
  font-size: @font-size-lg;
  color: @text-primary;
  font-weight: @font-weight-medium;
}
.speech-draft-step__loading-sub {
  font-size: @font-size-sm;
  color: @text-tertiary;
}

@keyframes speech-draft-bounce {
  0%, 80%, 100% {
    transform: translateY(0);
    opacity: 0.45;
  }
  40% {
    transform: translateY(-10px);
    opacity: 1;
  }
}
</style>
