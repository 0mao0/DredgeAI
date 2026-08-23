<template>
  <div class="meeting-info-step">
    <div class="meeting-info-step__toolbar">
      <AppButton size="sm" @click="historyOpen = true">历史记录</AppButton>
      <AppButton size="sm" @click="uploadOpen = true">上传施组方案</AppButton>
    </div>

    <div class="meeting-info-step__mic-zone">
      <button
        class="meeting-info-step__mic"
        :class="{ 'is-recording': recording }"
        type="button"
        @pointerdown="onPress"
        @pointerup="onRelease"
        @pointerleave="onRelease"
        @pointercancel="onRelease"
      >
        <AudioOutlined />
      </button>
      <div class="meeting-info-step__caption">
        {{ recording ? '松开结束录音' : '按住说话，说出今日计划' }}
      </div>
      <div v-if="asrLoading" class="meeting-info-step__hint">正在识别…首次识别需加载模型，请稍候</div>
      <div v-else-if="asrError" class="meeting-info-step__hint is-error">{{ asrError }}</div>
    </div>

    <a-textarea
      v-model:value="planText"
      class="meeting-info-step__plan"
      :rows="4"
      placeholder="今日计划将在这里显示，可编辑"
    />

    <div class="meeting-info-step__tags">
      <div class="meeting-info-step__tags-title">推荐示例</div>
      <a-tag
        v-for="item in recommendedCases"
        :key="item.label"
        class="meeting-info-step__tag"
        color="blue"
        @click="planText = item.plan"
      >
        {{ item.label }}
      </a-tag>
    </div>

    <AppButton
      variant="primary"
      size="lg"
      block
      :loading="parsing"
      :disabled="!planText.trim()"
      @click="onParse"
    >
      下一步，整理信息
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
import { ref, watch } from 'vue'
import { AudioOutlined, InboxOutlined } from '@ant-design/icons-vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { MeetingHistoryDto } from '@/types'
import {
  getMeetingHistory,
  getKnowledgeDocumentStatus,
  transcribeAudio,
  uploadKnowledgeDocument,
} from '@/api/modules/aiMeeting'
import { convertToWav16k, extractErrorMessage } from '@/utils/audioToWav'
import { useRecorder } from '../composables/useRecorder'

const props = defineProps<{
  parsing: boolean
  plan: string
}>()
const emit = defineEmits<{
  'parse': [planText: string]
  'update:plan': [planText: string]
  'loadHistory': [id: string]
}>()

const recommendedCases = [
  {
    label: '基坑支护',
    plan: '今日任务：基坑支护施工，重点检查边坡稳定与临边防护；深基坑作业严格执行专项方案，雨前检查排水系统。',
  },
  {
    label: '钢筋模板',
    plan: '今日任务：钢筋绑扎与模板安装，作业前核对图纸与材料，绑扎间距按规范执行，模板支撑验收合格后再浇筑。',
  },
  {
    label: '混凝土浇筑',
    plan: '今日任务：混凝土浇筑，检查泵管与布料机固定，分层振捣密实并及时养护，浇筑过程中专人看护模板支撑。',
  },
  {
    label: '高处作业',
    plan: '今日任务：屋面与脚手架高处作业，作业前检查脚手架连墙件与防护栏杆，作业人员全程系挂安全带。',
  },
  {
    label: '临时用电',
    plan: '今日任务：现场临时用电检查与动火作业，配电箱上锁挂牌，动火前办理动火证并配备灭火器。',
  },
]

const planText = ref('')
watch(
  () => props.plan,
  (value) => {
    if (value !== planText.value) planText.value = value
  },
  { immediate: true },
)
watch(planText, (value) => emit('update:plan', value))

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
  asrError.value = ''
  try {
    const wav = await convertToWav16k(audio)
    const text = await transcribeAudio(wav)
    planText.value = [planText.value, text].filter(Boolean).join('\n')
  } catch (err) {
    asrError.value = `语音识别失败：${extractErrorMessage(err)}`
  } finally {
    asrLoading.value = false
  }
}

function onParse(): void {
  if (!planText.value.trim()) return
  emit('parse', planText.value.trim())
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
  margin-bottom: @spacing-xl;
}
.meeting-info-step__mic-zone {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-bottom: @spacing-xl;
}
.meeting-info-step__mic {
  width: 96px;
  height: 96px;
  border-radius: 50%;
  border: none;
  background: @brand-gradient;
  color: #fff;
  font-size: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: @shadow-brand;
  transition: transform @transition-fast;
  touch-action: none;

  &.is-recording {
    transform: scale(1.08);
    animation: meeting-mic-pulse 1.2s ease-in-out infinite;
  }
}
.meeting-info-step__caption {
  margin-top: @spacing-md;
  font-size: @font-size-base;
  color: @text-primary;
}
.meeting-info-step__hint {
  margin-top: @spacing-xs;
  font-size: @font-size-sm;
  color: @text-tertiary;

  &.is-error {
    color: @danger;
  }
}
.meeting-info-step__plan {
  margin-bottom: @spacing-lg;
}
.meeting-info-step__tags {
  margin-bottom: @spacing-xl;
}
.meeting-info-step__tags-title {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.meeting-info-step__tags :deep(.ant-tag) {
  margin-bottom: @spacing-xs;
}
.meeting-info-step__tag {
  cursor: pointer;
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

@keyframes meeting-mic-pulse {
  0%, 100% { box-shadow: @shadow-brand; }
  50% { box-shadow: 0 0 0 10px color-mix(in srgb, var(--color-brand) 20%, transparent); }
}
</style>
