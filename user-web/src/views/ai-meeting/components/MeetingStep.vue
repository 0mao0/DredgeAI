<template>
  <SectionCard title="会议进行中" flush>
    <a-alert
      type="info"
      show-icon
      :message="recording ? '全程录音中…' : '录音未开始'"
    />
    <div v-if="speechText" class="meeting-step__speech">
      <div class="meeting-step__speech-label">晨会稿朗读</div>
      <SpeechPlayer :text="speechText" auto-play />
    </div>
    <p class="meeting-step__tip">
      会议结束后，系统会自动将录音转写并整理成报告（含出勤、转写稿、问答记录）。
    </p>
    <AppButton variant="primary" size="lg" block :loading="loading" @click="onFinish">
      结束会议并生成报告
    </AppButton>
  </SectionCard>
</template>

<script setup lang="ts">
import { onMounted, onScopeDispose } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { useRecorder } from '../composables/useRecorder'
import SpeechPlayer from './SpeechPlayer.vue'

defineProps<{
  loading: boolean
  speechText: string
}>()
const emit = defineEmits<{
  finish: [recording: Blob]
}>()

const { recording, start: startRecording, stop: stopRecording } = useRecorder()

onMounted(() => {
  void startRecording()
})
onScopeDispose(() => {
  void stopRecording()
})

async function onFinish(): Promise<void> {
  const audio = await stopRecording()
  emit('finish', audio)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.meeting-step__speech {
  margin: @spacing-lg 0;
}
.meeting-step__speech-label {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.meeting-step__tip {
  font-size: @font-size-sm;
  color: @text-tertiary;
  margin-bottom: @spacing-lg;
}
</style>
