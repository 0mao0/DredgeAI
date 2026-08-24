<template>
  <a-drawer
    :open="open"
    title="新人录入"
    placement="right"
    :width="360"
    @close="onClose"
  >
    <div class="worker-enroll__section">
      <div class="worker-enroll__section-title">身份证识别</div>
      <a-upload-dragger
        v-if="cameraMode !== 'idcard' && !idCardPreview"
        accept="image/*"
        :show-upload-list="false"
        :before-upload="onUploadIdCard"
        :disabled="idCardLoading"
        class="worker-enroll__upload"
      >
        <p class="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p class="ant-upload-text">上传身份证照片（点击或拖拽）</p>
        <p class="ant-upload-hint" />
      </a-upload-dragger>
      <div v-if="idCardPreview" class="worker-enroll__preview">
        <img :src="idCardPreview" class="worker-enroll__preview-img" alt="身份证照片">
        <AppButton size="sm" variant="text" @click="onResetIdCard">重新上传</AppButton>
      </div>
      <a-button type="link" block class="worker-enroll__camera-link" @click="toggleCamera('idcard')">
        {{ cameraMode === 'idcard' ? '收起摄像头' : '使用摄像头拍摄身份证' }}
      </a-button>
      <AppButton
        v-if="cameraMode === 'idcard'"
        variant="primary"
        block
        :loading="idCardLoading"
        @click="onCaptureIdCard"
      >
        拍照识别身份证
      </AppButton>
      <div v-if="idCard" class="worker-enroll__fields">
        <div><span>姓名</span>{{ idCard.name || '—' }}</div>
        <div><span>身份证号</span>{{ idCard.idCardNumber || '—' }}</div>
        <div><span>性别/民族</span>{{ idCard.gender || '—' }} / {{ idCard.nation || '—' }}</div>
      </div>
    </div>

    <div class="worker-enroll__section">
      <div class="worker-enroll__section-title">人脸录入</div>
      <a-form layout="vertical" class="worker-enroll__form">
        <a-form-item label="姓名">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="班组">
          <a-input v-model:value="form.team" placeholder="如：钢筋班" />
        </a-form-item>
      </a-form>
      <template v-if="cameraMode === 'face'">
        <AppButton variant="primary" block :loading="faceLoading" @click="onCaptureFace">
          拍照录入人脸
        </AppButton>
      </template>
      <AppButton v-else block @click="toggleCamera('face')">打开摄像头拍人脸</AppButton>
      <div v-if="facePreview" class="worker-enroll__preview">
        <img :src="facePreview" class="worker-enroll__preview-img" alt="人脸照片">
      </div>
    </div>

    <video
      v-if="stream && cameraMode"
      ref="videoRef"
      class="worker-enroll__video"
      :src-object="stream"
      autoplay
      playsinline
      muted
    />
    <a-result
      v-else-if="error && cameraMode"
      status="warning"
      title="无法访问摄像头"
      :sub-title="error"
    />

    <AppButton
      variant="primary"
      size="lg"
      block
      :loading="submitting"
      :disabled="!canSubmit"
      class="worker-enroll__submit"
      @click="onSubmit"
    >
      确认录入
    </AppButton>
    <a-alert
      v-if="enrolled"
      type="success"
      show-icon
      class="worker-enroll__done"
      message="录入成功"
      :description="`${enrolled.name} 已加入人脸库，点名时可直接识别`"
    />
    <AppButton
      v-if="enrolled"
      size="lg"
      block
      class="worker-enroll__next"
      @click="onClose"
    >
      完成
    </AppButton>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, nextTick, onScopeDispose, ref, watch } from 'vue'
import { InboxOutlined } from '@ant-design/icons-vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { IdCardRecognitionDto, WorkerDto } from '@/types'
import { createWorker, enrollWorkerFace, recognizeIdCard } from '@/api/modules/aiMeeting'
import { useCamera } from '../composables/useCamera'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  'enrolled': [worker: WorkerDto]
}>()

type CameraMode = 'idcard' | 'face' | null

const cameraMode = ref<CameraMode>(null)
const videoRef = ref<HTMLVideoElement | null>(null)
const { stream, error, start, stop, capturePhoto } = useCamera()
const idCardLoading = ref(false)
const faceLoading = ref(false)
const submitting = ref(false)
const idCard = ref<IdCardRecognitionDto | null>(null)
const idCardPreview = ref<string | null>(null)
const facePreview = ref<string | null>(null)
const facePhoto = ref<Blob | null>(null)
const enrolled = ref<WorkerDto | null>(null)
const form = ref({ name: '', team: '' })

const canSubmit = computed(() =>
  Boolean(form.value.name.trim() && (idCard.value?.idCardNumber || idCardPreview.value) && facePhoto.value),
)

watch(
  () => props.open,
  (open) => {
    if (open) {
      cameraMode.value = null
      idCard.value = null
      idCardPreview.value = null
      facePreview.value = null
      facePhoto.value = null
      enrolled.value = null
      form.value = { name: '', team: '' }
    } else {
      stop()
    }
  },
  { immediate: true },
)

watch(stream, async (s) => {
  if (!s) return
  await nextTick()
  if (videoRef.value) {
    videoRef.value.srcObject = s
    try {
      await videoRef.value.play()
    } catch {
      // 自动播放被拦截时等待用户手势
    }
  }
})

onScopeDispose(() => stop())

async function toggleCamera(mode: Exclude<CameraMode, null>): Promise<void> {
  if (cameraMode.value === mode) {
    cameraMode.value = null
    stop()
    return
  }
  cameraMode.value = mode
  if (!stream.value) {
    await start()
  }
}

async function onUploadIdCard(file: File): Promise<boolean> {
  idCardLoading.value = true
  try {
    idCardPreview.value = await blobToDataUrl(file)
    idCard.value = await recognizeIdCard(file)
    form.value.name = idCard.value.name
  } catch {
    idCard.value = null
  } finally {
    idCardLoading.value = false
  }
  return false
}

async function onCaptureIdCard(): Promise<void> {
  if (!stream.value) {
    const ok = await start()
    if (!ok) return
  }
  if (!videoRef.value) return
  idCardLoading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    idCardPreview.value = await blobToDataUrl(photo)
    idCard.value = await recognizeIdCard(photo)
    form.value.name = idCard.value.name
  } catch {
    idCard.value = null
  } finally {
    idCardLoading.value = false
  }
}

async function onCaptureFace(): Promise<void> {
  if (!stream.value) {
    const ok = await start()
    if (!ok) return
  }
  if (!videoRef.value) return
  faceLoading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    facePhoto.value = photo
    facePreview.value = await blobToDataUrl(photo)
  } finally {
    faceLoading.value = false
  }
}

async function onSubmit(): Promise<void> {
  if (!canSubmit.value || !facePhoto.value) return
  submitting.value = true
  try {
    const worker = await createWorker({
      name: form.value.name.trim(),
      employeeNo: idCard.value?.idCardNumber || `TMP-${Date.now()}`,
      team: form.value.team.trim(),
    })
    await enrollWorkerFace(worker.id, facePhoto.value)
    enrolled.value = { ...worker, faceStatus: 'enrolled' }
    emit('enrolled', enrolled.value)
  } finally {
    submitting.value = false
  }
}

function onResetIdCard(): void {
  idCardPreview.value = null
  idCard.value = null
}

function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(blob)
  })
}

function onClose(): void {
  emit('update:open', false)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.worker-enroll__section {
  margin-bottom: @spacing-xl;
}
.worker-enroll__section-title {
  font-size: @font-size-sm;
  font-weight: @font-weight-semibold;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.worker-enroll__video {
  width: 100%;
  border-radius: @radius-base;
  margin-bottom: @spacing-md;
}
.worker-enroll__fields {
  margin-top: @spacing-md;
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-base;

  > div {
    display: flex;
    gap: @spacing-sm;
    padding: @spacing-xs 0;
    font-size: @font-size-sm;

    span {
      flex: 0 0 72px;
      color: @text-secondary;
    }
  }
}
.worker-enroll__upload {
  margin-bottom: @spacing-md;

  :deep(.ant-upload-drag) {
    width: 100%;
    height: auto;
    aspect-ratio: 1.7778;
    padding: 0 @spacing-md;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: @spacing-sm;
  }
  :deep(.ant-upload-drag-icon) {
    margin-bottom: 0;

    .anticon {
      font-size: 22px;
    }
  }
  :deep(.ant-upload-text) {
    font-size: @font-size-sm;
  }
  :deep(.ant-upload-hint) {
    display: none;
  }
}
.worker-enroll__camera-link {
  margin-bottom: @spacing-md;
}
.worker-enroll__preview {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-md;
}
.worker-enroll__preview-img {
  width: 96px;
  height: 60px;
  object-fit: cover;
  border-radius: @radius-base;
  border: 1px solid @border-color;
}
.worker-enroll__form {
  margin-top: @spacing-sm;
}
.worker-enroll__submit {
  margin-top: @spacing-sm;
}
.worker-enroll__done {
  margin-top: @spacing-md;
}
.worker-enroll__next {
  margin-top: @spacing-md;
}
</style>
