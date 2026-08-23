<template>
  <div class="ai-meeting-page">
    <PageHeader title="AI晨会" description="录入 → 确认 → 晨会稿 → 点名 → 会议 → 报告" />
    <MeetingSteps :items="steps" :current="current" @go="handleGoStep" />
    <MeetingInfoStep
      v-if="current === 0"
      v-model:plan="planText"
      :parsing="parsing"
      @parse="handleParse"
      @load-history="handleLoadHistory"
    />
    <PlanConfirmStep
      v-else-if="current === 1"
      :plan="planResult"
      :loading="loading"
      @submit="handleCreate"
      @back="current = 0"
    />
    <SpeechDraftStep
      v-else-if="current === 2"
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
      v-else-if="current === 3"
      :loading="loading"
      :list="attendance"
      :count="peopleCount"
      @capture="handleCapture"
      @done="current = 4"
    />
    <MeetingStep
      v-else-if="current === 4"
      :loading="loading"
      :qa-records="qaRecords"
      @ask-text="handleAskText"
      @ask-audio="handleAskAudio"
      @finish="handleMeetingFinish"
    />
    <ReportStep v-else-if="current === 5" :report="report" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import type {
  AttendanceItemDto,
  MeetingRecordDto,
  PlanParseResult,
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
  parsePlan,
  recognizeAttendance,
  saveSpeechDraft,
  startMeeting,
  uploadMeetingRecording,
} from '@/api/modules/aiMeeting'
import MeetingInfoStep from './components/MeetingInfoStep.vue'
import PlanConfirmStep from './components/PlanConfirmStep.vue'
import SpeechDraftStep from './components/SpeechDraftStep.vue'
import AttendanceStep from './components/AttendanceStep.vue'
import MeetingStep from './components/MeetingStep.vue'
import ReportStep from './components/ReportStep.vue'
import MeetingSteps from './components/MeetingSteps.vue'
import { useAudioPlayer } from './composables/useAudioPlayer'
import { extractErrorMessage } from '@/utils/audioToWav'

const steps = ['录入', '确认', '晨会稿', '点名', '会议', '报告']
const current = ref(0)
const meeting = ref<MeetingRecordDto | null>(null)
const loading = ref(false)
const draft = ref<SpeechDraftDto | null>(null)
const attendance = ref<AttendanceItemDto[]>([])
const qaRecords = ref<QaRecordDto[]>([])
const report = ref<ReportDto | null>(null)
const peopleCount = ref<number | null>(null)
const planText = ref('')
const planResult = ref<PlanParseResult | null>(null)
const parsing = ref(false)
const { playing, play, stop: stopAudio } = useAudioPlayer()
const audioLoading = ref(false)

async function handleParse(planText: string): Promise<void> {
  parsing.value = true
  try {
    planResult.value = await parsePlan(planText)
    current.value = 1
  } catch (err) {
    message.error(`整理失败：${extractErrorMessage(err)}`)
  } finally {
    parsing.value = false
  }
}

function handleGoStep(index: number): void {
  if (index < current.value) current.value = index
}

async function handleCreate(preInfo: PreInfo): Promise<void> {
  loading.value = true
  try {
    meeting.value = await createMeeting(preInfo)
    planText.value = ''
    planResult.value = null
    current.value = 2
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
    planResult.value = null
    planText.value = ''
    if (record.preInfoJson) {
      try {
        const p = JSON.parse(record.preInfoJson) as Partial<PreInfo> & { date?: string }
        planText.value = p.tasks ?? ''
      } catch {
        planText.value = ''
      }
    }
    current.value
      = record.status === 'rollcall'
        ? 3
        : record.status === 'ongoing'
          ? 4
          : record.status === 'completed'
            ? 5
            : record.speechDraft
              ? 2
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
    current.value = 3
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
    current.value = 5
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
</style>
