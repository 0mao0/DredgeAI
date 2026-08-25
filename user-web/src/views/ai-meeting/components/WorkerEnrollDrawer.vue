<template>
  <a-drawer
    :open="open"
    title="新人录入"
    placement="right"
    :width="560"
    :body-style="{ padding: '20px' }"
    @close="onClose"
  >
    <div class="worker-enroll">
      <div class="worker-enroll__section">
        <div class="worker-enroll__section-title">A · 现场采集</div>
        <div class="worker-enroll__camera">
          <video
            v-if="stream"
            ref="videoRef"
            class="worker-enroll__video"
            :src-object="stream"
            autoplay
            playsinline
            muted
          />
          <div v-else-if="starting" class="worker-enroll__camera-hint">
            正在启用摄像头...
          </div>
          <a-result
            v-else-if="error"
            status="warning"
            title="无法访问摄像头"
            :sub-title="error"
          />
          <div v-if="stream" class="worker-enroll__camera-actions">
            <AppButton size="sm" :loading="idCardLoading" @click="captureIdCardFromCamera">
              识别手持身份证
            </AppButton>
          </div>
        </div>
      </div>

      <div class="worker-enroll__section">
        <div class="worker-enroll__section-title">B · 身份证上传</div>
        <a-upload-dragger
          v-if="!idCardPreview"
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
          <p class="ant-upload-hint">也可以回到上方摄像头，手持身份证点击“识别手持身份证”</p>
        </a-upload-dragger>
        <div v-else class="worker-enroll__id-preview">
          <img :src="idCardPreview" class="worker-enroll__id-img" alt="身份证照片">
          <div v-if="idCardLoading" class="worker-enroll__img-mask">
            <LoadingOutlined spin />
            <span>正在识别身份证...</span>
          </div>
          <AppButton
            size="sm"
            variant="text"
            class="worker-enroll__reupload"
            @click="onResetIdCard"
          >
            <ReloadOutlined /> 重新上传
          </AppButton>
        </div>
      </div>

      <div class="worker-enroll__section">
        <div class="worker-enroll__section-title">C · 识别结果</div>
        <div v-if="idCardLoading" class="worker-enroll__loading">
          <LoadingOutlined spin />
          <span>正在识别身份证，请稍候...</span>
        </div>

        <div class="worker-enroll__photos">
          <div class="worker-enroll__photo">
            <div class="worker-enroll__photo-label">身份证照片</div>
            <img v-if="idCardPreview" :src="idCardPreview" class="worker-enroll__photo-img" alt="身份证照片">
            <div v-else class="worker-enroll__photo-empty">尚未上传</div>
          </div>
          <div class="worker-enroll__photo worker-enroll__photo--face" @click="captureFace">
            <div class="worker-enroll__photo-label">
              人脸截图
              <span v-if="facePreview" class="worker-enroll__retake-tip">点击重拍</span>
            </div>
            <img v-if="facePreview" :src="facePreview" class="worker-enroll__photo-img" alt="人脸照片">
            <div v-else class="worker-enroll__photo-empty">
              <CameraOutlined />
              <span>点击拍照</span>
            </div>
            <div v-if="faceLoading" class="worker-enroll__img-mask">
              <LoadingOutlined spin />
            </div>
          </div>
        </div>

        <a-form layout="vertical" class="worker-enroll__form">
          <a-row :gutter="12">
            <a-col :span="12">
              <a-form-item label="姓名">
                <a-input v-model:value="form.name" placeholder="请输入姓名" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="身份证号">
                <a-input v-model:value="form.idCardNumber" placeholder="请输入身份证号" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="民族">
                <a-input v-model:value="form.nation" placeholder="如：汉族" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item label="性别">
                <a-input v-model:value="form.gender" placeholder="如：男" />
              </a-form-item>
            </a-col>
            <a-col :span="24">
              <a-form-item label="班组">
                <a-input v-model:value="form.team" placeholder="如：钢筋班（可选）" />
              </a-form-item>
            </a-col>
          </a-row>
        </a-form>

        <div class="worker-enroll__db-info">
          <div class="worker-enroll__db-title">数据库信息</div>
          <a-row :gutter="12">
            <a-col :span="12">
              <div class="worker-enroll__db-item"><span>单位</span>{{ form.unit || '暂无' }}</div>
            </a-col>
            <a-col :span="12">
              <div class="worker-enroll__db-item"><span>工种</span>{{ form.job || '暂无' }}</div>
            </a-col>
          </a-row>
        </div>
      </div>

      <div class="worker-enroll__section worker-enroll__section--footer">
        <a-alert
          v-if="duplicate"
          type="warning"
          show-icon
          class="worker-enroll__alert"
          :message="duplicateByBirthday ? '该姓名与出生日期已存在（证件号有差异）' : '该身份证号已存在'"
          :description="`已存在工人「${existingWorker?.name ?? ''}」，确认入库后将覆盖其面部信息。`"
        />
        <a-alert
          v-if="submitError"
          type="error"
          show-icon
          class="worker-enroll__alert"
          message="入库失败"
          :description="submitError"
        />
        <a-alert
          v-if="enrolled"
          type="success"
          show-icon
          class="worker-enroll__alert"
          message="录入成功"
          :description="`${enrolled.name} 已加入人脸库，点名时可直接识别。`"
        />
        <div class="worker-enroll__actions">
          <AppButton size="lg" :disabled="submitting" @click="onClose">取消</AppButton>
          <AppButton
            variant="primary"
            size="lg"
            :loading="submitting"
            :disabled="!canSubmit"
            @click="onSubmit"
          >
            确认入库
          </AppButton>
        </div>
      </div>
    </div>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, nextTick, onScopeDispose, reactive, ref, watch } from 'vue'
import {
  CameraOutlined,
  InboxOutlined,
  LoadingOutlined,
  ReloadOutlined,
} from '@ant-design/icons-vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { IdCardRecognitionDto, WorkerDto } from '@/types'
import {
  createWorker,
  enrollWorkerFace,
  getWorkers,
  recognizeIdCard,
} from '@/api/modules/aiMeeting'
import { extractErrorMessage } from '@/utils/audioToWav'
import { birthdayFromIdCard } from '@/utils/attendanceName'
import { useCamera } from '../composables/useCamera'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{
  'update:open': [open: boolean]
  'enrolled': [worker: WorkerDto]
}>()

const videoRef = ref<HTMLVideoElement | null>(null)
const { stream, error, starting, start, stop, capturePhoto } = useCamera()

const idCardLoading = ref(false)
const faceLoading = ref(false)
const submitting = ref(false)
const idCardPreview = ref<string | null>(null)
const facePreview = ref<string | null>(null)
const facePhoto = ref<Blob | null>(null)
const enrolled = ref<WorkerDto | null>(null)
const duplicate = ref(false)
const duplicateByBirthday = ref(false)
const existingWorker = ref<WorkerDto | null>(null)
const submitError = ref('')

const form = reactive({
  name: '',
  idCardNumber: '',
  nation: '',
  gender: '',
  team: '',
  unit: '',
  job: '',
})

const canSubmit = computed(() =>
  Boolean(
    form.name.trim()
    && form.idCardNumber.trim()
    && facePhoto.value,
  ),
)

watch(
  () => props.open,
  (open) => {
    if (open) {
      reset()
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
  await waitForVideoReady()
  await captureFace()
})

onScopeDispose(() => stop())

async function waitForVideoReady(): Promise<void> {
  for (let i = 0; i < 20; i++) {
    if (videoRef.value && videoRef.value.videoWidth > 0) return
    await new Promise((resolve) => setTimeout(resolve, 200))
  }
}

async function captureFace(): Promise<void> {
  if (!stream.value) {
    const ok = await start()
    if (!ok) return
    await nextTick()
    await waitForVideoReady()
  }
  if (!videoRef.value) return
  faceLoading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    facePhoto.value = photo
    facePreview.value = await blobToDataUrl(photo)
  } catch (err) {
    submitError.value = `截图失败：${extractErrorMessage(err)}`
  } finally {
    faceLoading.value = false
  }
}

async function captureIdCardFromCamera(): Promise<void> {
  if (!stream.value) {
    const ok = await start()
    if (!ok) return
    await nextTick()
    await waitForVideoReady()
  }
  if (!videoRef.value) return
  idCardLoading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    idCardPreview.value = await blobToDataUrl(photo)
    await recognizeAndFill(photo)
  } catch (err) {
    submitError.value = `身份证识别失败：${extractErrorMessage(err)}`
  } finally {
    idCardLoading.value = false
  }
}

async function onUploadIdCard(file: File): Promise<boolean> {
  idCardLoading.value = true
  submitError.value = ''
  try {
    idCardPreview.value = await blobToDataUrl(file)
    await recognizeAndFill(file)
  } catch (err) {
    idCardPreview.value = null
    submitError.value = `身份证识别失败：${extractErrorMessage(err)}`
  } finally {
    idCardLoading.value = false
  }
  return false
}

async function recognizeAndFill(image: Blob): Promise<void> {
  const dto: IdCardRecognitionDto = await recognizeIdCard(image)
  form.name = dto.name || form.name
  form.idCardNumber = dto.idCardNumber || form.idCardNumber
  form.nation = dto.nation || form.nation
  form.gender = dto.gender || form.gender
  await checkDuplicate(form.idCardNumber)
}

async function checkDuplicate(employeeNo: string): Promise<void> {
  duplicate.value = false
  duplicateByBirthday.value = false
  existingWorker.value = null
  if (!employeeNo) return
  try {
    const workers = await getWorkers()
    let found = workers.find((worker) => worker.employeeNo === employeeNo) ?? null
    if (!found && form.name.trim()) {
      const birthday = birthdayFromIdCard(employeeNo)
      if (birthday) {
        const sameName = workers.filter((worker) => worker.name === form.name.trim())
        found = sameName.find((worker) => birthdayFromIdCard(worker.employeeNo) === birthday) ?? null
        if (found) duplicateByBirthday.value = true
      }
    }
    if (found) {
      duplicate.value = true
      existingWorker.value = found
    }
  } catch {
    // 读取历史工人失败时不影响录入流程
  }
}

async function onSubmit(): Promise<void> {
  if (!canSubmit.value || !facePhoto.value) return
  submitting.value = true
  submitError.value = ''
  try {
    const worker = await createWorker({
      name: form.name.trim(),
      employeeNo: form.idCardNumber.trim(),
      team: form.team.trim(),
    })
    await enrollWorkerFace(worker.id, facePhoto.value)
    enrolled.value = { ...worker, faceStatus: 'enrolled' }
    emit('enrolled', enrolled.value)
  } catch (err) {
    submitError.value = extractErrorMessage(err)
  } finally {
    submitting.value = false
  }
}

function onResetIdCard(): void {
  idCardPreview.value = null
  form.name = ''
  form.idCardNumber = ''
  form.nation = ''
  form.gender = ''
  duplicate.value = false
  duplicateByBirthday.value = false
  existingWorker.value = null
}

function reset(): void {
  idCardPreview.value = null
  facePreview.value = null
  facePhoto.value = null
  enrolled.value = null
  duplicate.value = false
  duplicateByBirthday.value = false
  existingWorker.value = null
  submitError.value = ''
  Object.assign(form, {
    name: '',
    idCardNumber: '',
    nation: '',
    gender: '',
    team: '',
    unit: '',
    job: '',
  })
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

:deep(.ant-drawer-content-wrapper) {
  max-width: 100%;
}

.worker-enroll {
  display: flex;
  flex-direction: column;
  gap: @spacing-lg;
}
.worker-enroll__section {
  min-width: 0;
}
.worker-enroll__section-title {
  font-size: @font-size-sm;
  font-weight: @font-weight-semibold;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.worker-enroll__camera {
  position: relative;
  min-height: 220px;
  background: #000;
  border-radius: @radius-base;
  overflow: hidden;
}
.worker-enroll__video {
  width: 100%;
  height: 260px;
  object-fit: cover;
  display: block;
}
.worker-enroll__camera-hint {
  height: 260px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: @text-tertiary;
  background: @content-bg;
}
.worker-enroll__camera-actions {
  position: absolute;
  left: @spacing-sm;
  bottom: @spacing-sm;
}
.worker-enroll__upload {
  :deep(.ant-upload-drag) {
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
    font-size: @font-size-xs;
    color: @text-tertiary;
    white-space: normal;
  }
}
.worker-enroll__id-preview {
  position: relative;
  border-radius: @radius-base;
  overflow: hidden;
  background: @content-bg;
}
.worker-enroll__id-img {
  width: 100%;
  height: 260px;
  object-fit: contain;
  display: block;
  background: @content-bg;
}
.worker-enroll__reupload {
  position: absolute;
  top: @spacing-sm;
  right: @spacing-sm;
  color: #fff;
  background: color-mix(in srgb, #000 45%, transparent);
  border: none;
  border-radius: @radius-sm;
  padding: 2px @spacing-sm;
  font-size: @font-size-xs;

  &:hover {
    background: color-mix(in srgb, #000 60%, transparent);
  }
}
.worker-enroll__img-mask {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: @spacing-sm;
  color: #fff;
  background: color-mix(in srgb, #000 45%, transparent);
  font-size: @font-size-sm;
}
.worker-enroll__loading {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  color: @text-secondary;
  font-size: @font-size-sm;
  margin-bottom: @spacing-md;
}
.worker-enroll__photos {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: @spacing-md;
  margin-bottom: @spacing-md;
}
.worker-enroll__photo {
  position: relative;
  border-radius: @radius-base;
  overflow: hidden;
  background: @content-bg;
  min-height: 130px;

  &--face {
    cursor: pointer;
  }
}
.worker-enroll__photo-label {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  font-size: @font-size-xs;
  color: @text-secondary;
  padding: @spacing-xs @spacing-sm;
  background: @bg-elevated;
  border-bottom: 1px solid @divider-color;
}
.worker-enroll__retake-tip {
  margin-left: auto;
  color: @brand-primary;
  font-weight: @font-weight-medium;
}
.worker-enroll__photo-img {
  width: 100%;
  height: 170px;
  object-fit: cover;
  display: block;
}
.worker-enroll__photo-empty {
  height: 170px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-xs;
  color: @text-tertiary;
  font-size: @font-size-xs;

  .anticon {
    font-size: 24px;
  }
}
.worker-enroll__form {
  margin-bottom: @spacing-sm;

  :deep(.ant-form-item) {
    margin-bottom: @spacing-sm;
  }
}
.worker-enroll__db-info {
  padding: @spacing-md;
  background: @content-bg;
  border-radius: @radius-base;
}
.worker-enroll__db-title {
  font-size: @font-size-xs;
  color: @text-secondary;
  margin-bottom: @spacing-sm;
}
.worker-enroll__db-item {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  font-size: @font-size-sm;
  color: @text-primary;

  span {
    flex: 0 0 40px;
    color: @text-secondary;
  }
}
.worker-enroll__section--footer {
  padding-top: @spacing-md;
  border-top: 1px solid @divider-color;
}
.worker-enroll__alert {
  margin-bottom: @spacing-md;
}
.worker-enroll__actions {
  display: flex;
  gap: @spacing-sm;

  > * {
    flex: 1;
  }
}

@media (max-width: 520px) {
  .worker-enroll__video,
  .worker-enroll__camera-hint {
    height: 200px;
  }
  .worker-enroll__id-img {
    height: 200px;
  }
  .worker-enroll__photos {
    grid-template-columns: 1fr;
  }
}
</style>
