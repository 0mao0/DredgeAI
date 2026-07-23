<template>
  <a-modal
    :open="open"
    :width="500"
    :footer="null"
    :closable="false"
    destroy-on-close
    class="voice-register-modal"
    @cancel="emit('update:open', false)"
  >
    <!-- Header -->
    <div class="modal-header">
      <div class="modal-header__icon">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
          <path d="M12 2C10.34 2 9 3.34 9 5V11C9 12.66 10.34 14 12 14C13.66 14 15 12.66 15 11V5C15 3.34 13.66 2 12 2Z" fill="currentColor" fill-opacity="0.85"/>
          <path d="M20 11C20 15.08 16.42 18.24 12.5 18.88V22H11.5V18.88C7.58 18.24 4 15.08 4 11H5.5C5.5 14.31 8.69 17 12 17C15.31 17 18.5 14.31 18.5 11H20Z" fill="currentColor" fill-opacity="0.6"/>
        </svg>
      </div>
      <h2 class="modal-header__title">创建我的音色</h2>
      <button class="modal-header__close" @click="emit('update:open', false)">
        <svg width="14" height="14" viewBox="0 0 16 16" fill="none">
          <path d="M4 4L12 12M12 4L4 12" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
        </svg>
      </button>
    </div>

    <!-- Tabs -->
    <div class="tabs">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        class="tab"
        :class="{ 'tab--active': activeTab === tab.key }"
        @click="activeTab = tab.key"
      >
        <component :is="tab.icon" class="tab__icon" />
        <span>{{ tab.label }}</span>
      </button>
    </div>

    <transition name="tab-fade" mode="out-in">
      <div class="content" :key="activeTab">
        <!-- 朗读录制 -->
        <div v-if="activeTab === 'record'">
          <div class="read-card">
            <p class="read-card__text">{{ recordText }}</p>
            <span class="read-card__dur">
              <svg width="10" height="10" viewBox="0 0 24 24" fill="none">
                <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>
                <path d="M12 6V12L16 14" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
              ~15秒
            </span>
          </div>

          <div class="record-area">
            <div class="record-row">
              <button
                class="mic-btn"
                :class="{ 'mic-btn--rec': recording }"
                :title="recording ? '点击停止' : '点击录音'"
                @click="toggleRecord"
              >
                <span class="mic-btn__ring" />
                <svg v-if="!recording" width="18" height="18" viewBox="0 0 24 24" fill="none">
                  <path d="M12 2C10.34 2 9 3.34 9 5V11C9 12.66 10.34 14 12 14C13.66 14 15 12.66 15 11V5C15 3.34 13.66 2 12 2Z" fill="currentColor"/>
                  <path d="M20 11C20 15.08 16.42 18.24 12.5 18.88V22H11.5V18.88C7.58 18.24 4 15.08 4 11H5.5C5.5 14.31 8.69 17 12 17C15.31 17 18.5 14.31 18.5 11H20Z" fill="currentColor" fill-opacity="0.7"/>
                </svg>
                <svg v-else width="18" height="18" viewBox="0 0 24 24" fill="none">
                  <rect x="6" y="4" width="4" height="16" rx="1" fill="currentColor"/>
                  <rect x="14" y="4" width="4" height="16" rx="1" fill="currentColor"/>
                </svg>
              </button>

              <div class="wave" :class="{ 'wave--on': recording }">
                <div v-for="i in 28" :key="i" class="wave__bar" :style="waveStyle(i)" />
              </div>

              <div class="timer" :class="{ 'timer--on': recording }">
                {{ formattedTime }}<span class="timer__max">/ 00:15</span>
              </div>
            </div>
          </div>

          <transition name="slide-up">
            <div v-if="recordedBlob" class="file-card" @click="togglePlayRecorded">
              <svg v-if="!playing" class="file-card__play" width="14" height="14" viewBox="0 0 24 24" fill="none">
                <path d="M8 5V19L19 12L8 5Z" fill="currentColor"/>
              </svg>
              <svg v-else class="file-card__play" width="14" height="14" viewBox="0 0 24 24" fill="none">
                <rect x="6" y="4" width="4" height="16" rx="1" fill="currentColor"/>
                <rect x="14" y="4" width="4" height="16" rx="1" fill="currentColor"/>
              </svg>
              <span>录音完成 · 00:{{ String(Math.floor(recordElapsed)).padStart(2, '0') }}</span>
              <button class="file-card__del" @click.stop="recordedBlob = null; audioChunks = []">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor"/></svg>
              </button>
            </div>
          </transition>
        </div>

        <!-- 上传文件 -->
        <div v-else>
          <div
            class="upload-zone"
            :class="{ 'upload-zone--over': isDragOver }"
            @dragover.prevent="isDragOver = true"
            @dragleave.prevent="isDragOver = false"
            @drop.prevent="handleDrop"
            @click="triggerFileInput"
          >
            <input ref="fileInputRef" type="file" :accept="uploadAccept" style="display:none" @change="handleInputChange">
            <div class="upload-zone__icon" :class="{ 'upload-zone__icon--over': isDragOver }">
              <svg width="28" height="28" viewBox="0 0 24 24" fill="none">
                <path d="M14 2H6C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V8L14 2Z" fill="currentColor" fill-opacity="0.15" stroke="currentColor" stroke-width="1.5"/>
                <path d="M14 2V8H20" stroke="currentColor" stroke-width="1.5"/>
                <path d="M12 18V12M9 15H15" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
              </svg>
            </div>
            <p class="upload-zone__text"><span class="upload-zone__link">点击上传</span> 或拖拽文件</p>
            <p class="upload-zone__hint">.wav / .m4a / .mp3，最大 10MB</p>
          </div>

          <transition name="slide-up">
            <div v-if="uploadedFile" class="file-card" @click="togglePlayUploaded">
              <svg v-if="!playing" class="file-card__play" width="14" height="14" viewBox="0 0 24 24" fill="none">
                <path d="M8 5V19L19 12L8 5Z" fill="currentColor"/>
              </svg>
              <svg v-else class="file-card__play" width="14" height="14" viewBox="0 0 24 24" fill="none">
                <rect x="6" y="4" width="4" height="16" rx="1" fill="currentColor"/>
                <rect x="14" y="4" width="4" height="16" rx="1" fill="currentColor"/>
              </svg>
              <span>{{ uploadedFile.name }} · {{ formatFileSize(uploadedFile.size) }}</span>
              <button class="file-card__del" @click.stop="clearUploaded">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor"/></svg>
              </button>
            </div>
          </transition>
        </div>
      </div>
    </transition>

    <!-- Form Area -->
    <transition name="slide-up">
      <div v-if="hasAudio" class="form-area">
        <div class="form-row">
          <input v-model="form.name" class="form-input" placeholder="音色名称" maxlength="20">
          <div class="form-gender">
            <button
              v-for="g in genderOptions"
              :key="g.value"
              class="gen-btn"
              :class="{ 'gen-btn--on': form.gender === g.value }"
              @click="form.gender = g.value"
            >
              <component :is="g.icon" class="gen-btn__icon" />
              <span>{{ g.label }}</span>
            </button>
          </div>
        </div>

        <div class="form-actions">
          <button class="btn-text" @click="emit('update:open', false)">取消</button>
          <button
            class="btn-primary"
            :disabled="!canSubmit || submitting"
            @click="handleSubmit"
          >
            <LoadingOutlined v-if="submitting" spin />
            <svg v-else width="14" height="14" viewBox="0 0 24 24" fill="none">
              <path d="M5 12H19M12 5V19" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"/>
            </svg>
            <span>{{ submitting ? '上传中...' : '上传' }}</span>
          </button>
        </div>
      </div>
    </transition>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, h, onUnmounted } from 'vue'
import { CustomerServiceOutlined, LoadingOutlined, UploadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import type { VoiceItem } from '@/types'

const props = withDefaults(defineProps<{
  open: boolean
  initialTab?: 'record' | 'upload'
}>(), { initialTab: 'record' })
const emit = defineEmits<{
  'update:open': [value: boolean]
  confirmed: [payload: { voice: VoiceItem; formData: FormData }]
}>()

const activeTab = ref<'record' | 'upload'>(props.initialTab)
const uploadAccept = '.wav,.m4a,.mp3,audio/wav,audio/mp4,audio/mpeg'
const fileInputRef = ref<HTMLInputElement>()
const isDragOver = ref(false)

const tabs = [
  { key: 'record' as const, label: '朗读', icon: h(CustomerServiceOutlined) },
  { key: 'upload' as const, label: '上传', icon: h(UploadOutlined) },
]

const genderOptions = [
  { value: '男声' as const, label: '男', icon: h('span', { class: 'gen-icon' }, '♂') },
  { value: '女声' as const, label: '女', icon: h('span', { class: 'gen-icon' }, '♀') },
  { value: '童声' as const, label: '童', icon: h('span', { class: 'gen-icon' }, '♪') },
]

const recordText = '大家好，今天天气真不错。我来测试一下我的声音效果，希望录制顺利。这段录音会用来自动生成我的专属音色，以后就可以用自己的声音来配音了。'

const form = ref({
  name: '',
  gender: '男声' as '男声' | '女声' | '童声',
})

let mediaRecorder: MediaRecorder | null = null
let audioChunks: Blob[] = []
let recordTimer: ReturnType<typeof setInterval> | null = null
const recording = ref(false)
const recordElapsed = ref(0)
const recordedBlob = ref<Blob | null>(null)
let blobUrl: string | null = null

const uploadedFile = ref<File | null>(null)
let uploadedBlob: Blob | null = null

const playing = ref(false)
let playbackEl: HTMLAudioElement | null = null

const submitting = ref(false)

const hasAudio = computed(() => recordedBlob.value || uploadedFile.value)

const formattedTime = computed(() => {
  const m = Math.floor(recordElapsed.value / 60)
  const s = recordElapsed.value % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
})

const canSubmit = computed(() => {
  return (recordedBlob.value || uploadedFile.value) && !!form.value.name.trim()
})

function waveStyle(i: number) {
  if (!recording.value) {
    const h = 0.2 + (i % 5) * 0.12
    return { height: `${h * 100}%` }
  }
  return {}
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

async function toggleRecord(): Promise<void> {
  if (recording.value) { stopRecord(); return }
  if (recordedBlob.value) {
    recordedBlob.value = null
    audioChunks = []
  }
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
      ? 'audio/webm;codecs=opus'
      : 'audio/webm'
    mediaRecorder = new MediaRecorder(stream, { mimeType })
    audioChunks = []
    recordElapsed.value = 0

    mediaRecorder.ondataavailable = (e) => {
      if (e.data.size > 0) audioChunks.push(e.data)
    }
    mediaRecorder.onstop = () => {
      recordedBlob.value = new Blob(audioChunks, { type: mimeType })
      if (!form.value.name) {
        form.value.name = `我的声音 ${new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })}`
      }
      stream.getTracks().forEach(t => t.stop())
    }

    mediaRecorder.start(100)
    recording.value = true
    recordTimer = setInterval(() => {
      recordElapsed.value++
      if (recordElapsed.value >= 15) stopRecord()
    }, 1000)
  } catch {
    message.error('麦克风访问被拒绝，请在浏览器设置中允许麦克风权限')
  }
}

function stopRecord(): void {
  if (recordTimer) { clearInterval(recordTimer); recordTimer = null }
  if (mediaRecorder && mediaRecorder.state !== 'inactive') mediaRecorder.stop()
  recording.value = false
  mediaRecorder = null
}

function triggerFileInput(): void {
  fileInputRef.value?.click()
}

function handleDrop(e: DragEvent): void {
  isDragOver.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) processFile(file)
}

function handleInputChange(e: Event): void {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (file) processFile(file)
}

function processFile(file: File): void {
  const maxSize = 10 * 1024 * 1024
  if (file.size > maxSize) { message.warning('文件大小不能超过 10MB'); return }
  const validTypes = ['.wav', '.m4a', '.mp3']
  const ext = '.' + file.name.split('.').pop()?.toLowerCase()
  if (!validTypes.includes(ext)) { message.warning('请上传 .wav / .m4a / .mp3 格式文件'); return }
  uploadedFile.value = file
  uploadedBlob = file.slice(0, file.size, file.type)
  if (!form.value.name) {
    form.value.name = file.name.replace(/\.[^.]+$/, '').substring(0, 20)
  }
}

function clearUploaded(): void {
  uploadedFile.value = null
  uploadedBlob = null
}

function togglePlayRecorded(): void {
  if (!recordedBlob.value) return
  if (playing.value) { stopPlayback(); return }
  blobUrl = URL.createObjectURL(recordedBlob.value)
  startPlayback(blobUrl)
}

function togglePlayUploaded(): void {
  if (!uploadedBlob) return
  if (playing.value) { stopPlayback(); return }
  blobUrl = URL.createObjectURL(uploadedBlob)
  startPlayback(blobUrl)
}

function startPlayback(url: string): void {
  playbackEl = new Audio(url)
  playbackEl.addEventListener('ended', () => { stopPlayback(); URL.revokeObjectURL(url) })
  playbackEl.play()
  playing.value = true
}

function stopPlayback(): void {
  if (playbackEl) { playbackEl.pause(); playbackEl = null }
  playing.value = false
}

function handleSubmit(): void {
  if (submitting.value) return
  const audioBlob = recordedBlob.value || uploadedBlob
  if (!audioBlob || !form.value.name.trim()) return

  submitting.value = true

  const fd = new FormData()
  fd.append('file', audioBlob, recordedBlob.value ? 'recording.webm' : uploadedFile.value!.name)
  fd.append('name', form.value.name.trim())
  fd.append('gender', form.value.gender)

  const pendingVoice: VoiceItem = {
    id: `temp_${Date.now()}`,
    name: form.value.name.trim(),
    gender: form.value.gender,
    category: '通用',
    style: '自定义音色',
    provider: '自定义',
    visibility: 'private',
    userId: 'local_user',
    createdAt: new Date().toISOString(),
    uploadStatus: 'converting',
  }

  emit('confirmed', { voice: pendingVoice, formData: fd })
  emit('update:open', false)
}

onUnmounted(() => {
  stopPlayback()
  if (blobUrl) URL.revokeObjectURL(blobUrl)
  if (mediaRecorder) mediaRecorder.stream?.getTracks().forEach(t => t.stop())
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.voice-register-modal {
  :deep(.ant-modal-content) {
    background: @card-bg;
    border: 1px solid @border-color;
    border-radius: @radius-lg;
    padding: 0;
    overflow: hidden;
    box-shadow: @shadow-lg;
  }
  :deep(.ant-modal-header) { display: none; }
  :deep(.ant-modal-body) { padding: 0; }
  :deep(.ant-modal-footer) { display: none; }
}

/* Header */
.modal-header {
  display: flex; align-items: center; gap: 8px;
  padding: 12px 12px 0;
  &__icon {
    width: 28px; height: 28px;
    border-radius: @radius-sm;
    background: color-mix(in srgb, @brand-primary 12%, transparent);
    display: flex; align-items: center; justify-content: center;
    color: @brand-primary;
    flex-shrink: 0;
  }
  &__title {
    flex: 1;
    font-size: @font-size-base;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    margin: 0;
  }
  &__close {
    flex-shrink: 0;
    width: 28px; height: 28px;
    border: none; background: transparent;
    border-radius: 6px;
    display: flex; align-items: center; justify-content: center;
    color: @text-tertiary;
    cursor: pointer;
    padding: 0;
    &:hover { background: @surface-hover; color: @text-primary; }
  }
}

/* Tabs */
.tabs {
  display: flex; gap: 2px;
  margin: 12px 12px 0;
}
.tab {
  flex: 1;
  display: flex; align-items: center; justify-content: center; gap: 5px;
  padding: 8px;
  border: none;
  background: transparent;
  border-radius: @radius-sm @radius-sm 0 0;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-tertiary;
  cursor: pointer;
  transition: all @transition-fast;
  border-bottom: 2px solid transparent;
  &__icon { font-size: 13px; }
  &:hover { color: @text-primary; background: @surface-hover; }
  &--active {
    color: @brand-primary;
    border-bottom-color: @brand-primary;
  }
}

/* Content */
.content {
  padding: 12px 12px;
}

.tab-fade-enter-active,
.tab-fade-leave-active {
  transition: opacity 0.15s ease;
}
.tab-fade-enter-from,
.tab-fade-leave-to { opacity: 0; }

/* Read Card */
.read-card {
  background: color-mix(in srgb, @brand-primary 5%, transparent);
  border: 1px solid color-mix(in srgb, @brand-primary 10%, transparent);
  border-radius: @radius-base;
  padding: 10px 14px;
  margin-bottom: 14px;
  &__text {
    font-size: @font-size-sm;
    color: @text-primary;
    line-height: 1.7;
    margin: 0 0 6px;
  }
  &__dur {
    display: inline-flex; align-items: center; gap: 4px;
    font-size: 11px;
    color: @text-tertiary;
  }
}

/* Record Area */
.record-area {
  padding: 0;
}

.record-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;
}

.timer {
  flex-shrink: 0;
  white-space: nowrap;
  font-size: 22px;
  font-weight: @font-weight-semibold;
  font-variant-numeric: tabular-nums;
  color: @text-primary;
  letter-spacing: 0.5px;
  &--on { color: @danger; }
  &__max {
    font-size: @font-size-xs;
    color: @text-tertiary;
    margin-left: 3px;
    font-weight: @font-weight-regular;
  }
}

.wave {
  flex-shrink: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 2px; height: 36px;
  &__bar {
    width: 3px;
    min-height: 3px;
    border-radius: 2px;
    background: @border-color;
    transition: background 0.2s;
    flex-shrink: 0;
  }
  &--on &__bar {
    background: linear-gradient(to top, @brand-primary, @accent);
    animation: w 0.5s ease-in-out infinite alternate;
  }
}

@keyframes w {
  0% { transform: scaleY(0.3); }
  100% { transform: scaleY(1); }
}

.mic-btn {
  position: relative;
  flex-shrink: 0;
  width: 42px; height: 42px;
  padding: 0;
  border: none;
  background: linear-gradient(135deg, @brand-primary, @accent);
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  cursor: pointer;
  color: #fff;
  transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
  box-shadow: 0 3px 10px color-mix(in srgb, @brand-primary 25%, transparent);
  &__ring {
    position: absolute; inset: -3px; border-radius: 50%;
    border: 2px solid color-mix(in srgb, @brand-primary 15%, transparent);
    opacity: 0; transition: opacity 0.2s;
  }
  &:hover { transform: scale(1.05); box-shadow: 0 4px 14px color-mix(in srgb, @brand-primary 35%, transparent); }
  &:active { transform: scale(0.95); }
  &--rec {
    background: @danger;
    box-shadow: 0 3px 10px color-mix(in srgb, @danger 30%, transparent);
    animation: pulse 1.2s ease-in-out infinite;
    .mic-btn__ring {
      opacity: 1;
      animation: ring 1.2s ease-in-out infinite;
    }
  }
  svg { display: block; }
}

@keyframes pulse {
  0%, 100% { box-shadow: 0 3px 10px color-mix(in srgb, @danger 30%, transparent); }
  50% { box-shadow: 0 3px 20px color-mix(in srgb, @danger 45%, transparent); }
}
@keyframes ring {
  0% { transform: scale(1); opacity: 0.5; }
  100% { transform: scale(1.3); opacity: 0; }
}

/* File Card */
.file-card {
  display: flex; align-items: center; gap: 8px;
  margin-top: 10px;
  padding: 8px 12px;
  background: color-mix(in srgb, @success 8%, transparent);
  border: 1px solid color-mix(in srgb, @success 18%, transparent);
  border-radius: @radius-base;
  font-size: @font-size-sm;
  color: @text-primary;
  cursor: pointer;
  transition: background 0.15s;
  &:hover { background: color-mix(in srgb, @success 12%, transparent); }
  &__play { flex-shrink: 0; color: @success; }
  span { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  &__del {
    flex-shrink: 0;
    width: 22px; height: 22px;
    border: none; background: transparent;
    border-radius: 4px;
    display: flex; align-items: center; justify-content: center;
    color: @text-tertiary; cursor: pointer;
    &:hover { background: color-mix(in srgb, @danger 10%, transparent); color: @danger; }
  }
}

.slide-up-enter-active { animation: sl 0.2s cubic-bezier(0.34, 1.56, 0.64, 1); }
.slide-up-leave-active { transition: opacity 0.12s, transform 0.12s; }
.slide-up-enter-from { opacity: 0; transform: translateY(-6px); }
.slide-up-leave-to { opacity: 0; transform: translateY(-4px); }
@keyframes sl { from { opacity: 0; transform: translateY(-6px); } to { opacity: 1; transform: translateY(0); } }

/* Upload Zone */
.upload-zone {
  border: 2px dashed @border-color;
  border-radius: @radius-base;
  padding: 28px 20px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  background: color-mix(in srgb, @brand-primary 2%, transparent);
  &:hover {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 5%, transparent);
  }
  &--over {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 8%, transparent);
    border-style: solid;
  }
  &__icon {
    width: 48px; height: 48px; margin: 0 auto 10px;
    border-radius: @radius-base;
    background: color-mix(in srgb, @brand-primary 6%, transparent);
    display: flex; align-items: center; justify-content: center;
    color: @text-tertiary;
    transition: all 0.2s;
    &--over { color: @brand-primary; background: color-mix(in srgb, @brand-primary 12%, transparent); transform: scale(1.08); }
  }
  &__text { font-size: @font-size-sm; color: @text-secondary; margin: 0 0 4px; }
  &__link { color: @brand-primary; font-weight: @font-weight-medium; }
  &__hint { font-size: 11px; color: @text-tertiary; margin: 0; }
}

/* Form Area */
.form-area {
  border-top: 1px solid @divider-color;
  padding: 10px 12px 6px;
}

.form-row {
  display: flex; align-items: center; gap: 8px;
  margin-bottom: 12px;
}

.form-gender {
  display: flex; gap: 6px; flex-shrink: 0;
}

.form-actions {
  display: flex; align-items: center; justify-content: flex-end; gap: 8px;
}

.form-input {
  flex: 1;
  padding: 8px 10px;
  border: 1.5px solid @border-color;
  border-radius: @radius-sm;
  font-size: @font-size-sm;
  color: @text-primary;
  background: transparent;
  outline: none;
  min-width: 0;
  box-sizing: border-box;
  &::placeholder { color: @text-tertiary; }
  &:focus { border-color: @brand-primary; box-shadow: 0 0 0 2px color-mix(in srgb, @brand-primary 10%, transparent); }
  &:hover { border-color: @text-tertiary; }
}

.gen-btn {
  display: inline-flex; align-items: center; gap: 3px;
  padding: 6px 10px;
  border: 1.5px solid @border-color;
  border-radius: @radius-sm;
  background: transparent;
  font-size: @font-size-xs;
  color: @text-tertiary;
  cursor: pointer;
  transition: all @transition-fast;
  &__icon { font-size: 12px; }
  &:hover { border-color: @brand-primary; color: @brand-primary; }
  &--on {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 8%, transparent);
    color: @brand-primary;
    font-weight: @font-weight-medium;
  }
}

.btn-text {
  padding: 7px 12px;
  border: none;
  border-radius: @radius-sm;
  background: transparent;
  font-size: @font-size-sm;
  color: @text-secondary;
  cursor: pointer;
  white-space: nowrap;
  &:hover { background: @surface-hover; color: @text-primary; }
}

.btn-primary {
  display: inline-flex; align-items: center; gap: 5px;
  padding: 7px 16px;
  border: none;
  border-radius: @radius-sm;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: #fff;
  background: linear-gradient(135deg, @brand-primary, @accent);
  cursor: pointer;
  white-space: nowrap;
  transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
  box-shadow: 0 2px 6px color-mix(in srgb, @brand-primary 20%, transparent);
  &:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 3px 12px color-mix(in srgb, @brand-primary 30%, transparent); }
  &:active:not(:disabled) { transform: translateY(0); }
  &:disabled { opacity: 0.35; cursor: not-allowed; }
}

/* Reduced Motion */
@media (prefers-reduced-motion: reduce) {
  .wave__bar { animation: none !important; }
  .mic-btn--rec { animation: none; }
  .mic-btn__ring { display: none; }
  .tab-fade-enter-active, .tab-fade-leave-active,
  .slide-up-enter-active, .slide-up-leave-active { transition: none; }
  .slide-up-enter-from, .slide-up-leave-to { opacity: 1; transform: none; }
  .tab-fade-enter-from, .tab-fade-leave-to { opacity: 1; }
}
</style>
