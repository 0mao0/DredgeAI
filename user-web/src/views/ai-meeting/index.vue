<template>
  <div class="ai-meeting-page">
    <PageHeader title="AI晨会">
      <template #extra>
        <AppButton
          v-if="current === 0"
          size="sm"
          variant="text"
          @click="historyOpen = true"
        >
          <HistoryOutlined /> 历史记录
        </AppButton>
      </template>
    </PageHeader>
    <MeetingSteps :items="steps" :current="current" @go="handleGoStep" />
    <MeetingInfoStep
      v-if="current === 0"
      v-model:plan="planText"
      v-model:history-open="historyOpen"
      :parsing="parsing"
      :projects="projects"
      :selected-project-id="selectedProjectId"
      @parse="handleParse"
      @load-history="handleLoadHistory"
      @update:selected-project-id="selectedProjectId = $event"
      @project-created="handleProjectCreated"
      @project-deleted="handleProjectDeleted"
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
      :streaming="streaming"
      :streaming-text="streamingText"
      :date="meeting?.date"
      :meeting-id="meeting?.id"
      @generate="handleGenerateSpeech"
      @save="handleSaveDraft"
      @confirm="handleConfirmDraft"
    />
    <AttendanceStep
      v-else-if="current === 3"
      :loading="loading"
      :list="attendance"
      :unrecognized="unrecognizedFaces"
      :count="peopleCount"
      :speech-text="draft?.content ?? ''"
      :meeting-id="meeting?.id"
      @capture="handleCapture"
      @done="current = 4"
    />
    <MeetingStep
      v-else-if="current === 4"
      :loading="loading"
      @finish="handleMeetingFinish"
    />
    <ReportStep v-else-if="current === 5" :report="report" :draft-content="draft?.content ?? ''" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { message } from 'ant-design-vue'
import { HistoryOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type {
  AttendanceItemDto,
  MeetingProjectDto,
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
  getMeeting,
  getMeetingProjects,
  getReport,
  parsePlan,
  recognizeAttendance,
  saveSpeechDraft,
  startMeeting,
  streamSpeechDraft,
  uploadUnrecognizedFaces,
  uploadMeetingRecording,
} from '@/api/modules/aiMeeting'
import type { UnrecognizedFaceCrop } from '@/api/modules/aiMeeting'
import MeetingInfoStep from './components/MeetingInfoStep.vue'
import PlanConfirmStep from './components/PlanConfirmStep.vue'
import SpeechDraftStep from './components/SpeechDraftStep.vue'
import AttendanceStep from './components/AttendanceStep.vue'
import MeetingStep from './components/MeetingStep.vue'
import ReportStep from './components/ReportStep.vue'
import MeetingSteps from './components/MeetingSteps.vue'
import { extractErrorMessage } from '@/utils/audioToWav'
import { cropFaceFromPhoto } from '@/utils/faceCrop'

const steps = ['录入', '确认', '晨会稿', '点名', '会议', '报告']
const current = ref(0)
const historyOpen = ref(false)
const meeting = ref<MeetingRecordDto | null>(null)
const loading = ref(false)
const draft = ref<SpeechDraftDto | null>(null)
/** 晨会稿流式生成中（边生成边显示文字） */
const streaming = ref(false)
const streamingText = ref('')
const attendance = ref<AttendanceItemDto[]>([])
const unrecognizedFaces = ref<AttendanceItemDto[]>([])
const qaRecords = ref<QaRecordDto[]>([])
const report = ref<ReportDto | null>(null)
const peopleCount = ref<number | null>(null)
const projects = ref<MeetingProjectDto[]>([])
const selectedProjectId = ref<string | undefined>(undefined)
const planText = ref('')
const planResult = ref<PlanParseResult | null>(null)
const parsing = ref(false)
let capturing = false
const uploadedFaceSignatures = new Set<string>()

const selectedProject = computed(
  () => projects.value.find((p) => p.id === selectedProjectId.value) ?? null,
)

onMounted(async () => {
  try {
    projects.value = await getMeetingProjects()
    if (!selectedProjectId.value && projects.value.length > 0) {
      selectedProjectId.value = projects.value[0]!.id
    }
  } catch {
    // 项目列表加载失败不阻塞晨会流程
  }
})

function handleProjectCreated(project: MeetingProjectDto): void {
  projects.value = [project, ...projects.value.filter((p) => p.id !== project.id)]
  selectedProjectId.value = project.id
}

function handleProjectDeleted(id: string): void {
  projects.value = projects.value.filter((p) => p.id !== id)
  if (selectedProjectId.value === id) {
    selectedProjectId.value = projects.value[0]?.id
  }
}

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
    const payload: PreInfo = {
      ...preInfo,
      projectName: selectedProject.value?.name ?? '',
      projectSummary: selectedProject.value?.summary ?? '',
    }
    meeting.value = await createMeeting(payload)
    uploadedFaceSignatures.clear()
    planText.value = ''
    planResult.value = null
    // 立刻进入晨会稿页，在该页流式显示生成内容，不在确认页干等
    current.value = 2
    try {
      await streamSpeechDraftFlow()
    } catch (err) {
      // 自动生成失败时留在晨会稿页，由页面上的“生成晨会稿”按钮重试
      message.warning(`晨会稿自动生成失败：${extractErrorMessage(err)}，可在晨会稿页点击重试`)
    }
  } finally {
    loading.value = false
  }
}

/** 流式生成晨会稿：边生成边渲染文字，结束后落库为正式 draft */
async function streamSpeechDraftFlow(): Promise<void> {
  if (!meeting.value) return
  streaming.value = true
  streamingText.value = ''
  try {
    await streamSpeechDraft(meeting.value.id, (delta) => {
      streamingText.value += delta
    })
    const content = streamingText.value
    streamingText.value = ''
    draft.value = { id: meeting.value.id, content, status: 'generated', updatedAt: new Date().toISOString() }
  } catch (err) {
    streamingText.value = ''
    draft.value = null
    throw err
  } finally {
    streaming.value = false
  }
}

async function handleLoadHistory(id: string): Promise<void> {
  loading.value = true
  try {
    const record = await getMeeting(id)
    meeting.value = record
    uploadedFaceSignatures.clear()
    streaming.value = false
    streamingText.value = ''
    draft.value = record.speechDraft ?? null
    const { recognized, unrecognized } = splitAttendance(record.attendance)
    attendance.value = recognized
    unrecognizedFaces.value = unrecognized
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
    await streamSpeechDraftFlow()
  } catch (err) {
    message.error(`晨会稿生成失败：${extractErrorMessage(err)}`)
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
  // 上一轮识别未结束时跳过本次拍照，避免并发请求导致后端竞态重复入库
  if (!meeting.value || capturing) return
  capturing = true
  loading.value = true
  try {
    const res = await recognizeAttendance(meeting.value.id, photo)
    const { recognized, unrecognized } = splitAttendance(res.faces)
    attendance.value = recognized
    unrecognizedFaces.value = unrecognized
    peopleCount.value = res.count
    void uploadUnrecognizedCrops(photo, unrecognized)
  } catch (err) {
    message.error(`识别失败：${extractErrorMessage(err)}`)
  } finally {
    capturing = false
    loading.value = false
  }
}

/** 把未识别人脸按 bbox 裁剪后上传存档（同一位置的只传一次），供会后报告展示与后续入库 */
async function uploadUnrecognizedCrops(photo: Blob, faces: AttendanceItemDto[]): Promise<void> {
  if (!meeting.value) return
  const crops: UnrecognizedFaceCrop[] = []
  for (const face of faces) {
    const bbox = face.bbox
    if (!bbox || bbox.length !== 4) continue
    const signature = bbox.map((v) => Math.round(v)).join(',')
    if (uploadedFaceSignatures.has(signature)) continue
    try {
      const blob = await cropFaceFromPhoto(photo, bbox)
      crops.push({ blob, confidence: face.confidence ?? 0, bbox })
      uploadedFaceSignatures.add(signature)
    } catch {
      // 单张裁剪失败跳过
    }
  }
  if (crops.length === 0) return
  try {
    await uploadUnrecognizedFaces(meeting.value.id, crops)
  } catch {
    // 上传失败不阻塞点名流程
  }
}

/** 已识别人脸：按 workerId 去重，保留置信度更高的一次（同名不同 workerId 视为两个人，不做人名合并） */
function dedupeAttendance(list: AttendanceItemDto[]): AttendanceItemDto[] {
  const byKey = new Map<string, AttendanceItemDto>()
  for (const item of list) {
    const key = item.workerId ? `worker:${item.workerId}` : `anon:${item.name}`
    const prev = byKey.get(key)
    if (!prev || (item.confidence ?? 0) > (prev.confidence ?? 0)) {
      byKey.set(key, item)
    }
  }
  return [...byKey.values()]
}

/** 拆分已识别/未识别人脸：已识别按 workerId 去重；未识别人脸按 bbox 位置去重 */
function splitAttendance(list: AttendanceItemDto[]): {
  recognized: AttendanceItemDto[]
  unrecognized: AttendanceItemDto[]
} {
  return {
    recognized: dedupeAttendance(list.filter((item) => Boolean(item.workerId))),
    unrecognized: dedupeUnrecognized(list.filter((item) => !item.workerId)),
  }
}

function dedupeUnrecognized(list: AttendanceItemDto[]): AttendanceItemDto[] {
  const kept: AttendanceItemDto[] = []
  for (const item of list) {
    const dup = kept.findIndex((k) => sameUnknownFace(k, item))
    if (dup >= 0) {
      if ((item.confidence ?? 0) > (kept[dup]?.confidence ?? 0)) kept[dup] = item
    } else {
      kept.push(item)
    }
  }
  return kept
}

function sameUnknownFace(a: AttendanceItemDto, b: AttendanceItemDto): boolean {
  const ab = a.bbox
  const bb = b.bbox
  if (ab?.length === 4 && bb?.length === 4) {
    return bboxIoU(ab, bb) >= 0.35
  }
  // 无 bbox（旧接口/测试数据）：按置信度近似收敛
  return Math.round((a.confidence ?? 0) * 100) === Math.round((b.confidence ?? 0) * 100)
}

function bboxIoU(a: number[], b: number[]): number {
  const x1 = Math.max(a[0]!, b[0]!)
  const y1 = Math.max(a[1]!, b[1]!)
  const x2 = Math.min(a[2]!, b[2]!)
  const y2 = Math.min(a[3]!, b[3]!)
  const intersection = Math.max(0, x2 - x1) * Math.max(0, y2 - y1)
  const areaA = Math.max(0, a[2]! - a[0]!) * Math.max(0, a[3]! - a[1]!)
  const areaB = Math.max(0, b[2]! - b[0]!) * Math.max(0, b[3]! - b[1]!)
  const union = areaA + areaB - intersection
  return union <= 0 ? 0 : intersection / union
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
  width: 100%;
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
