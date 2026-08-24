<template>
  <div class="ai-meeting-page">
    <PageHeader title="AI晨会" description="录入 → 确认 → 晨会稿 → 点名 → 会议 → 报告" />
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
      :date="meeting?.date"
      @generate="handleGenerateSpeech"
      @save="handleSaveDraft"
      @confirm="handleConfirmDraft"
    />
    <AttendanceStep
      v-else-if="current === 3"
      :loading="loading"
      :list="attendance"
      :count="peopleCount"
      :speech-text="draft?.content ?? ''"
      @capture="handleCapture"
      @done="current = 4"
    />
    <MeetingStep
      v-else-if="current === 4"
      :loading="loading"
      :speech-text="draft?.content ?? ''"
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
  completeMeeting,
  createMeeting,
  generateSpeech,
  getMeeting,
  getReport,
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
import { extractErrorMessage } from '@/utils/audioToWav'

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

async function handleCreate(preInfo: PreInfo): Promise<void> {
  loading.value = true
  try {
    meeting.value = await createMeeting(preInfo)
    planText.value = ''
    planResult.value = null
    // 立刻进入晨会稿页，在该页显示生成中状态，不在确认页干等
    current.value = 2
    try {
      draft.value = await generateSpeech(meeting.value.id)
    } catch (err) {
      // 自动生成失败时进入晨会稿页，由页面上的“生成晨会稿”按钮重试
      draft.value = null
      message.warning(`晨会稿自动生成失败：${extractErrorMessage(err)}，可在晨会稿页点击重试`)
    }
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

async function handleCapture(photo: Blob): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    const res = await recognizeAttendance(meeting.value.id, photo)
    attendance.value = res.faces
    peopleCount.value = res.count
  } catch (err) {
    message.error(`识别失败：${extractErrorMessage(err)}`)
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

@media (max-width: 520px) {
  .ai-meeting-page {
    padding: @spacing-base;
  }
}
</style>
