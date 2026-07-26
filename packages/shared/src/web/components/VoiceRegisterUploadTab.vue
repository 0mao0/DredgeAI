<template>
  <div>
    <div
      class="upload-zone"
      :class="{ 'upload-zone--over': isDragOver }"
      @dragover.prevent="isDragOver = true"
      @dragleave.prevent="isDragOver = false"
      @drop.prevent="handleDrop"
      @click="triggerFileInput"
    >
      <input ref="fileInputRef" type="file" accept=".wav,.m4a,.mp3,audio/wav,audio/mp4,audio/mpeg" style="display:none" @change="handleInputChange">
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
</template>

<script setup lang="ts">
import { ref, onUnmounted } from 'vue'
import { message } from 'ant-design-vue'

const emit = defineEmits<{
  audioReady: [blob: Blob, fileName: string]
  cleared: []
}>()

const fileInputRef = ref<HTMLInputElement>()
const isDragOver = ref(false)
const uploadedFile = ref<File | null>(null)
let uploadedBlob: Blob | null = null

const playing = ref(false)
let playbackEl: HTMLAudioElement | null = null
let blobUrl: string | null = null

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
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
  emit('audioReady', uploadedBlob, file.name)
}

function clearUploaded(): void {
  uploadedFile.value = null
  uploadedBlob = null
  emit('cleared')
}

function togglePlayUploaded(): void {
  if (!uploadedBlob) return
  if (playing.value) { stopPlayback(); return }
  blobUrl = URL.createObjectURL(uploadedBlob)
  playbackEl = new Audio(blobUrl)
  playbackEl.addEventListener('ended', () => {
    stopPlayback()
    if (blobUrl) URL.revokeObjectURL(blobUrl)
  })
  playbackEl.play()
  playing.value = true
}

function stopPlayback(): void {
  if (playbackEl) { playbackEl.pause(); playbackEl = null }
  playing.value = false
}

onUnmounted(() => {
  stopPlayback()
  if (blobUrl) URL.revokeObjectURL(blobUrl)
})
</script>

<style scoped lang="less">
@import '../styles/variables.less';

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

@media (prefers-reduced-motion: reduce) {
  .slide-up-enter-active, .slide-up-leave-active { transition: none; }
  .slide-up-enter-from, .slide-up-leave-to { opacity: 1; transform: none; }
}
</style>
