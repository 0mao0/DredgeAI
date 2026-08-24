<template>
  <a-drawer
    :open="open"
    title="新人录入"
    placement="right"
    :width="360"
    @close="onClose"
  >
    <a-steps :current="step" size="small" class="worker-enroll__steps">
      <a-step title="拍身份证" />
      <a-step title="拍人脸" />
    </a-steps>

    <video
      v-if="stream"
      ref="videoRef"
      class="worker-enroll__video"
      :src-object="stream"
      autoplay
      playsinline
      muted
    />
    <a-result v-else-if="error" status="warning" title="无法访问摄像头" :sub-title="error" />

    <template v-if="step === 0">
      <p class="worker-enroll__hint">将身份证正面置于镜头前，点击拍照识别</p>
      <AppButton variant="primary" block :loading="idCardLoading" @click="onCaptureIdCard">
        拍照识别身份证
      </AppButton>
      <div class="worker-enroll__divider">或</div>
      <a-upload-dragger
        accept="image/*"
        :show-upload-list="false"
        :before-upload="onUploadIdCard"
        :disabled="idCardLoading"
        class="worker-enroll__upload"
      >
        <p class="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p class="ant-upload-text">上传身份证照片</p>
        <p class="ant-upload-hint">点击或拖拽图片，自动识别信息</p>
      </a-upload-dragger>
      <div v-if="idCard" class="worker-enroll__fields">
        <div><span>姓名</span>{{ idCard.name || '—' }}</div>
        <div><span>身份证号</span>{{ idCard.idCardNumber || '—' }}</div>
        <div><span>性别/民族</span>{{ idCard.gender || '—' }} / {{ idCard.nation || '—' }}</div>
        <div><span>出生日期</span>{{ idCard.birthDate || '—' }}</div>
        <div><span>住址</span>{{ idCard.address || '—' }}</div>
      </div>
      <a-alert
        v-if="idCard?.name && idCard?.idCardNumber"
        type="success"
        show-icon
        class="worker-enroll__id-ok"
        message="识别成功，请核对上方信息"
      />
      <AppButton
        v-if="idCard?.name && idCard?.idCardNumber"
        variant="primary"
        size="lg"
        block
        class="worker-enroll__next"
        @click="step = 1"
      >
        信息无误，下一步拍人脸
      </AppButton>
    </template>

    <template v-else>
      <a-form layout="vertical" class="worker-enroll__form">
        <a-form-item label="姓名">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="班组">
          <a-input v-model:value="form.team" placeholder="如：钢筋班" />
        </a-form-item>
      </a-form>
      <p class="worker-enroll__hint">将人脸对准镜头，点击录入（正脸、光线充足）</p>
      <AppButton variant="primary" block :loading="faceLoading" @click="onCaptureFace">
        拍照录入人脸
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
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { nextTick, onScopeDispose, ref, watch } from 'vue'
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

const step = ref(0)
const videoRef = ref<HTMLVideoElement | null>(null)
const { stream, error, start, stop, capturePhoto } = useCamera()
const idCardLoading = ref(false)
const faceLoading = ref(false)
const idCard = ref<IdCardRecognitionDto | null>(null)
const enrolled = ref<WorkerDto | null>(null)
const form = ref({ name: '', team: '' })

watch(
  () => props.open,
  (open) => {
    if (open) {
      step.value = 0
      idCard.value = null
      enrolled.value = null
      form.value = { name: '', team: '' }
      void start()
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

async function onUploadIdCard(file: File): Promise<boolean> {
  idCardLoading.value = true
  try {
    idCard.value = await recognizeIdCard(file)
    form.value.name = idCard.value.name
  } catch {
    idCard.value = { name: '', idCardNumber: '', gender: '', nation: '', birthDate: '', address: '', rawText: '' }
  } finally {
    idCardLoading.value = false
  }
  return false
}

async function onCaptureIdCard(): Promise<void> {
  if (!videoRef.value || !stream.value) return
  idCardLoading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    idCard.value = await recognizeIdCard(photo)
    form.value.name = idCard.value.name
  } catch {
    idCard.value = { name: '', idCardNumber: '', gender: '', nation: '', birthDate: '', address: '', rawText: '' }
  } finally {
    idCardLoading.value = false
  }
}

async function onCaptureFace(): Promise<void> {
  if (!videoRef.value || !stream.value) return
  if (!form.value.name.trim()) {
    return
  }
  faceLoading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    const worker = await createWorker({
      name: form.value.name.trim(),
      employeeNo: idCard.value?.idCardNumber || `TMP-${Date.now()}`,
      team: form.value.team.trim(),
    })
    await enrollWorkerFace(worker.id, photo)
    enrolled.value = { ...worker, faceStatus: 'enrolled' }
    emit('enrolled', enrolled.value)
  } finally {
    faceLoading.value = false
  }
}

function onClose(): void {
  emit('update:open', false)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.worker-enroll__steps {
  margin-bottom: @spacing-lg;
}
.worker-enroll__video {
  width: 100%;
  border-radius: @radius-base;
  margin-bottom: @spacing-md;
}
.worker-enroll__hint {
  color: @text-secondary;
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
.worker-enroll__next {
  margin-top: @spacing-lg;
}
.worker-enroll__divider {
  text-align: center;
  color: @text-tertiary;
  margin: @spacing-md 0;
  font-size: @font-size-sm;
}
.worker-enroll__upload {
  margin-bottom: @spacing-md;
}
.worker-enroll__id-ok {
  margin-top: @spacing-md;
}
.worker-enroll__form {
  margin-top: @spacing-sm;
}
.worker-enroll__done {
  margin-top: @spacing-md;
}
</style>
