<template>
  <div class="ai-meeting-page">
    <PageHeader title="AI晨会" description="会前录入 → 晨会稿 → 点名 → 会议 → 报告" />
    <a-steps :current="current" size="small" responsive>
      <a-step title="会前录入" />
      <a-step title="晨会稿" />
      <a-step title="点名" />
      <a-step title="会议" />
      <a-step title="报告" />
    </a-steps>
    <MeetingInfoStep
      v-if="current === 0"
      :loading="loading"
      :initial="initialForm"
      @submit="handleCreate"
      @load-history="handleLoadHistory"
    />
    <SpeechDraftStep
      v-else-if="current === 1"
      :draft="draft"
      :loading="loading"
      :playing="playing"
      :audio-loading="audioLoading"
      @generate="handleGenerateSpeech"
      @save="handleSaveDraft"
      @confirm="handleConfirmDraft"
      @play-audio="handlePlaySpeech"
      @stop-audio="stopAudio"
    />
    <AttendanceStep
      v-else-if="current === 2"
      :loading="loading"
      :list="attendance"
      :count="peopleCount"
      @capture="handleCapture"
      @done="current = 3"
    />
    <MeetingStep
      v-else-if="current === 3"
      :loading="loading"
      :qa-records="qaRecords"
      @ask-text="handleAskText"
      @ask-audio="handleAskAudio"
      @finish="handleMeetingFinish"
    />
    <ReportStep v-else-if="current === 4" :report="report" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import type {
  AttendanceItemDto,
  MeetingRecordDto,
  PreInfo,
  QaRecordDto,
  ReportDto,
  SpeechDraftDto,
} from '@/types'
import {
  askQa,
  askQaAudio,
  completeMeeting,
  createMeeting,
  generateSpeech,
  getMeeting,
  getReport,
  getSpeechAudio,
  recognizeAttendance,
  saveSpeechDraft,
  startMeeting,
  uploadMeetingRecording,
} from '@/api/modules/aiMeeting'
import MeetingInfoStep from './components/MeetingInfoStep.vue'
import SpeechDraftStep from './components/SpeechDraftStep.vue'
import AttendanceStep from './components/AttendanceStep.vue'
import MeetingStep from './components/MeetingStep.vue'
import ReportStep from './components/ReportStep.vue'
import { useAudioPlayer } from './composables/useAudioPlayer'

const current = ref(0)
const meeting = ref<MeetingRecordDto | null>(null)
const loading = ref(false)
const draft = ref<SpeechDraftDto | null>(null)
const attendance = ref<AttendanceItemDto[]>([])
const qaRecords = ref<QaRecordDto[]>([])
const report = ref<ReportDto | null>(null)
const peopleCount = ref<number | null>(null)
const initialForm = ref<PreInfo | null>(null)
const { playing, play, stop: stopAudio } = useAudioPlayer()
const audioLoading = ref(false)

async function handleCreate(preInfo: PreInfo): Promise<void> {
  loading.value = true
  try {
    meeting.value = await createMeeting(preInfo)
    initialForm.value = null
    current.value = 1
  } finally {
    loading.value = false
  }
}

async function handleLoadHistory(id: string): Promise<void> {
  loading.value = true
  try {
    const record = await getMeeting(id)
    meeting.value = record
    draft.value = record.speechDraft ?? null
    attendance.value = record.attendance
    qaRecords.value = record.qaRecords
    report.value = record.report ?? null
    peopleCount.value = null
    if (record.preInfoJson) {
      try {
        const p = JSON.parse(record.preInfoJson) as Partial<PreInfo> & { date?: string }
        initialForm.value = {
          date: p.date?.slice(0, 10) ?? '',
          weather: p.weather ?? '',
          tasks: p.tasks ?? '',
          riskPoints: p.riskPoints ?? '',
        }
      } catch {
        initialForm.value = null
      }
    }
    current.value
      = record.status === 'rollcall'
        ? 2
        : record.status === 'ongoing'
          ? 3
          : record.status === 'completed'
            ? 4
            : record.speechDraft
              ? 1
              : 0
  } finally {
    loading.value = false
  }
}

async function handleGenerateSpeech(): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    draft.value = await generateSpeech(meeting.value.id)
  } finally {
    loading.value = false
  }
}

async function handleSaveDraft(content: string): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    draft.value = await saveSpeechDraft(meeting.value.id, content)
  } finally {
    loading.value = false
  }
}

async function handleConfirmDraft(): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    if (draft.value) {
      draft.value = await saveSpeechDraft(meeting.value.id, draft.value.content)
    }
    meeting.value = await startMeeting(meeting.value.id)
    current.value = 2
  } finally {
    loading.value = false
  }
}

async function handlePlaySpeech(): Promise<void> {
  if (!meeting.value) return
  audioLoading.value = true
  try {
    const blob = await getSpeechAudio(meeting.value.id)
    play(blob)
  } finally {
    audioLoading.value = false
  }
}

async function handleCapture(photo: Blob): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    const res = await recognizeAttendance(meeting.value.id, photo)
    attendance.value = res.faces
    peopleCount.value = res.count
  } finally {
    loading.value = false
  }
}

async function handleAskText(question: string): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    qaRecords.value.push(await askQa(meeting.value.id, question))
  } finally {
    loading.value = false
  }
}

async function handleAskAudio(audio: Blob): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    qaRecords.value.push(await askQaAudio(meeting.value.id, audio))
  } finally {
    loading.value = false
  }
}

async function handleMeetingFinish(recording: Blob): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    if (recording.size > 0) {
      await uploadMeetingRecording(meeting.value.id, recording)
    }
    meeting.value = await completeMeeting(meeting.value.id)
    report.value = await getReport(meeting.value.id)
    current.value = 4
  } finally {
    loading.value = false
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.ai-meeting-page {
  max-width: 720px;
  margin: 0 auto;
  padding: @page-padding;
}
.ai-meeting-page :deep(.ant-steps) {
  margin-bottom: @spacing-lg;
}
</style>
