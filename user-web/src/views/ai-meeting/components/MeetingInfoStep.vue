<template>
  <div class="meeting-info-step">
    <div class="meeting-info-step__toolbar">
      <AppButton size="sm" @click="historyOpen = true">历史记录</AppButton>
      <AppButton size="sm" @click="uploadOpen = true">上传施组方案</AppButton>
    </div>

    <SectionCard title="语音录入今日计划" flush>
      <div class="meeting-info-step__mic">
        <button
          class="meeting-info-step__mic-btn"
          :class="{ 'is-recording': recording }"
          type="button"
          @pointerdown="onPress"
          @pointerup="onRelease"
          @pointerleave="onRelease"
          @pointercancel="onRelease"
        >
          <AudioOutlined />
          <span>{{ recording ? '松开发言' : '按住说话录入计划' }}</span>
        </button>
        <div v-if="asrLoading" class="meeting-info-step__hint">语音识别中，请稍候…</div>
        <div v-else-if="asrError" class="meeting-info-step__hint is-error">{{ asrError }}</div>
        <div v-else class="meeting-info-step__hint">说出今日任务与风险点，或点击下方推荐案例快速录入</div>
      </div>
      <a-textarea
        v-model:value="form.tasks"
        :rows="4"
        placeholder="今日计划（可编辑）"
      />
      <div class="meeting-info-step__tags">
        <a-tag
          v-for="tag in recommendedTags"
          :key="tag"
          class="meeting-info-step__tag"
          color="blue"
          @click="appendTag(tag)"
        >
          {{ tag }}
        </a-tag>
      </div>
    </SectionCard>

    <SectionCard title="补充信息" flush>
      <a-form layout="vertical">
        <a-form-item label="日期">
          <a-date-picker v-model:value="form.date" value-format="YYYY-MM-DD" style="width: 100%" />
        </a-form-item>
        <a-form-item label="天气">
          <a-input v-model:value="form.weather" placeholder="如：晴，28℃" />
        </a-form-item>
        <a-form-item label="风险点">
          <a-textarea v-model:value="form.riskPoints" :rows="2" placeholder="安全风险提示（可在语音中描述）" />
        </a-form-item>
      </a-form>
    </SectionCard>

    <AppButton variant="primary" size="lg" block :loading="loading" @click="onSubmit">
      保存并生成晨会稿
    </AppButton>

    <a-drawer
      v-model:open="historyOpen"
      title="历史晨会"
      placement="left"
      :width="320"
    >
      <a-spin :spinning="historyLoading">
        <a-empty v-if="!historyLoading && history.length === 0" description="暂无历史记录" />
        <div
          v-for="item in history"
          :key="item.id"
          class="meeting-info-step__history-item"
          @click="onPickHistory(item)"
        >
          <div class="meeting-info-step__history-title">{{ item.date }} · {{ item.taskPreview || '未填写任务' }}</div>
          <a-tag :color="statusColor(item.status)">{{ statusText(item.status) }}</a-tag>
        </div>
      </a-spin>
    </a-drawer>

    <a-drawer
      v-model:open="uploadOpen"
      title="上传施组方案（进知识库）"
      placement="right"
      :width="340"
    >
      <a-upload-dragger
        accept=".pdf,.doc,.docx"
        :show-upload-list="false"
        :before-upload="onBeforeUpload"
        :disabled="uploading"
      >
        <p class="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p class="ant-upload-text">点击或拖拽 PDF/Word 到此处</p>
        <p class="ant-upload-hint">上传后自动解析入库，晨会稿生成与问答即可引用</p>
      </a-upload-dragger>
      <div v-if="uploading" class="meeting-info-step__upload-status">
        <a-spin />
        <span>正在解析「{{ uploadingName }}」… {{ uploadProgress }}%</span>
      </div>
      <a-alert
        v-else-if="uploadDone"
        type="success"
        show-icon
        message="解析完成，已进入知识库"
      />
      <a-alert
        v-else-if="uploadError"
        type="error"
        show-icon
        :message="uploadError"
      />
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { AudioOutlined, InboxOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { MeetingHistoryDto, PreInfo } from '@/types'
import {
  getMeetingHistory,
  getKnowledgeDocumentStatus,
  transcribeAudio,
  uploadKnowledgeDocument,
} from '@/api/modules/aiMeeting'
import { useRecorder } from '../composables/useRecorder'

const props = defineProps<{
  loading: boolean
  initial?: PreInfo | null
}>()
const emit = defineEmits<{
  submit: [preInfo: PreInfo]
  loadHistory: [id: string]
}>()

const recommendedTags = [
  '今日任务：钢筋绑扎与模板安装',
  '今日任务：基坑支护施工，注意边坡稳定',
  '重点风险：高处作业，必须系挂安全带',
  '今日任务：混凝土浇筑，注意振捣与养护',
  '重点风险：临时用电与动火作业',
]

const form = reactive<PreInfo>({
  date: new Date().toISOString().slice(0, 10),
  weather: '',
  tasks: '',
  riskPoints: '',
})

watch(
  () => props.initial,
  (value) => {
    if (!value) return
    form.date = value.date || form.date
    form.weather = value.weather ?? ''
    form.tasks = value.tasks ?? ''
    form.riskPoints = value.riskPoints ?? ''
  },
  { immediate: true },
)

const { recording, start: startRecording, stop: stopRecording } = useRecorder()
const asrLoading = ref(false)
const asrError = ref('')

async function onPress(): Promise<void> {
  asrError.value = ''
  try {
    await startRecording()
  } catch {
    asrError.value = '无法访问麦克风，请检查浏览器权限'
  }
}

async function onRelease(): Promise<void> {
  if (!recording.value) return
  const audio = await stopRecording()
  if (audio.size === 0) return
  asrLoading.value = true
  try {
    const text = await transcribeAudio(audio)
    form.tasks = [form.tasks, text].filter(Boolean).join('\n')
  } catch {
    asrError.value = '语音识别失败，请重试或直接输入'
  } finally {
    asrLoading.value = false
  }
}

function appendTag(tag: string): void {
  const lines = form.tasks.split('\n').filter((l) => l.trim())
  if (!lines.includes(tag)) lines.push(tag)
  form.tasks = lines.join('\n')
}

function onSubmit(): void {
  emit('submit', { ...form })
}

const historyOpen = ref(false)
const historyLoading = ref(false)
const history = ref<MeetingHistoryDto[]>([])

watch(historyOpen, async (open) => {
  if (!open || history.value.length > 0) return
  historyLoading.value = true
  try {
    history.value = await getMeetingHistory()
  } finally {
    historyLoading.value = false
  }
})

function onPickHistory(item: MeetingHistoryDto): void {
  historyOpen.value = false
  emit('loadHistory', item.id)
}

const uploadOpen = ref(false)
const uploading = ref(false)
const uploadingName = ref('')
const uploadProgress = ref(0)
const uploadDone = ref(false)
const uploadError = ref('')

async function onBeforeUpload(file: File): Promise<boolean> {
  uploading.value = true
  uploadingName.value = file.name
  uploadProgress.value = 0
  uploadDone.value = false
  uploadError.value = ''
  try {
    const result = await uploadKnowledgeDocument(file)
    if (result.status?.state === 'succeeded') {
      uploadDone.value = true
      return false
    }
    await pollKnowledgeStatus(result.docId)
  } catch {
    uploadError.value = '上传失败，请稍后重试'
  } finally {
    uploading.value = false
  }
  return false
}

async function pollKnowledgeStatus(docId: string, tries = 60): Promise<void> {
  const status = await getKnowledgeDocumentStatus(docId)
  uploadProgress.value = status.progress ?? uploadProgress.value
  if (status.state === 'succeeded') {
    uploadDone.value = true
    return
  }
  if (status.state === 'failed' || status.state === 'partial') {
    uploadError.value = status.stageMessage ?? '解析失败，请重新上传'
    return
  }
  if (tries <= 0) {
    uploadError.value = '解析超时，请稍后查看知识库'
    return
  }
  await new Promise((r) => setTimeout(r, 2000))
  await pollKnowledgeStatus(docId, tries - 1)
}

function statusText(status: MeetingHistoryDto['status']): string {
  return {
    draft: '待生成稿',
    prepared: '已备稿',
    rollcall: '点名中',
    ongoing: '会议中',
    completed: '已完成',
  }[status]
}

function statusColor(status: MeetingHistoryDto['status']): string {
  return {
    draft: 'default',
    prepared: 'blue',
    rollcall: 'orange',
    ongoing: 'processing',
    completed: 'success',
  }[status]
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.meeting-info-step__toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: @spacing-md;
}
.meeting-info-step__mic {
  margin-bottom: @spacing-md;
}
.meeting-info-step__mic-btn {
  width: 100%;
  min-height: 88px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-sm;
  border: 1px dashed @border-color;
  border-radius: @radius-lg;
  background: @content-bg;
  color: @text-secondary;
  cursor: pointer;
  touch-action: none;
  font-size: @font-size-lg;
  transition: all 0.2s;

  &.is-recording {
    border-color: @danger;
    color: @danger;
    background: fade(@danger, 6%);
  }
}
.meeting-info-step__hint {
  margin-top: @spacing-sm;
  font-size: @font-size-sm;
  color: @text-secondary;

  &.is-error {
    color: @danger;
  }
}
.meeting-info-step__tags {
  display: flex;
  flex-wrap: wrap;
  gap: @spacing-xs;
  margin-top: @spacing-md;
}
.meeting-info-step__tag {
  cursor: pointer;
  margin-inline-end: 0;
}
.meeting-info-step__history-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border-radius: @radius-base;
  cursor: pointer;

  &:hover {
    background: @surface-hover;
  }
}
.meeting-info-step__history-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.meeting-info-step__upload-status {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-top: @spacing-md;
  color: @text-secondary;
}
</style>
