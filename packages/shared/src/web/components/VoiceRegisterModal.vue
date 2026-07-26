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
    <div class="modal-header">
      <div class="modal-header__icon">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
          <path d="M12 2C10.34 2 9 3.34 9 5V11C9 12.66 10.34 14 12 14C13.66 14 15 12.66 15 11V5C15 3.34 13.66 2 12 2Z" fill="currentColor" fill-opacity="0.85"/>
          <path d="M20 11C20 15.08 16.42 18.24 12.5 18.88V22H11.5V18.88C7.58 18.24 4 15.08 4 11H5.5C5.5 14.31 8.69 17 12 17C15.31 17 18.5 14.31 18.5 11H20Z" fill="currentColor" fill-opacity="0.6"/>
        </svg>
      </div>
      <h2 class="modal-header__title">{{ title }}</h2>
      <button class="modal-header__close" @click="emit('update:open', false)">
        <svg width="14" height="14" viewBox="0 0 16 16" fill="none">
          <path d="M4 4L12 12M12 4L4 12" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
        </svg>
      </button>
    </div>

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
        <VoiceRegisterRecordTab
          v-if="activeTab === 'record'"
          @audio-ready="(blob) => recordAudio = blob"
          @cleared="recordAudio = null"
        />
        <VoiceRegisterUploadTab
          v-else
          @audio-ready="(blob, name) => { uploadAudio = blob; uploadFileName = name }"
          @cleared="uploadAudio = null"
        />
      </div>
    </transition>

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
import { ref, computed, h } from 'vue'
import { CustomerServiceOutlined, LoadingOutlined, UploadOutlined } from '@ant-design/icons-vue'
import VoiceRegisterRecordTab from './VoiceRegisterRecordTab.vue'
import VoiceRegisterUploadTab from './VoiceRegisterUploadTab.vue'
import type { VoiceItem } from '@shared/types'

const props = withDefaults(defineProps<{
  open: boolean
  initialTab?: 'record' | 'upload'
  title?: string
}>(), {
  initialTab: 'record',
  title: '创建我的音色',
})
const emit = defineEmits<{
  'update:open': [value: boolean]
  confirmed: [payload: { voice: VoiceItem; formData: FormData }]
}>()

const activeTab = ref<'record' | 'upload'>(props.initialTab)
const recordAudio = ref<Blob | null>(null)
const uploadAudio = ref<Blob | null>(null)
const uploadFileName = ref('recording.webm')

const tabs = [
  { key: 'record' as const, label: '朗读', icon: h(CustomerServiceOutlined) },
  { key: 'upload' as const, label: '上传', icon: h(UploadOutlined) },
]

const genderOptions = [
  { value: '男声' as const, label: '男', icon: h('span', { class: 'gen-icon' }, '♂') },
  { value: '女声' as const, label: '女', icon: h('span', { class: 'gen-icon' }, '♀') },
  { value: '童声' as const, label: '童', icon: h('span', { class: 'gen-icon' }, '♪') },
]

const form = ref({
  name: '',
  gender: '男声' as '男声' | '女声' | '童声',
})

const submitting = ref(false)

const hasAudio = computed(() => {
  if (activeTab.value === 'record') return recordAudio.value !== null
  return uploadAudio.value !== null
})

const canSubmit = computed(() => {
  const audio = activeTab.value === 'record' ? recordAudio.value : uploadAudio.value
  return audio !== null && !!form.value.name.trim()
})

function handleSubmit(): void {
  if (submitting.value) return
  const audioBlob = activeTab.value === 'record' ? recordAudio.value : uploadAudio.value
  const name = form.value.name.trim()
  if (!audioBlob || !name) return

  submitting.value = true

  const fd = new FormData()
  fd.append('file', audioBlob, activeTab.value === 'record' ? 'recording.webm' : uploadFileName.value)
  fd.append('name', name)
  fd.append('gender', form.value.gender)

  const pendingVoice: VoiceItem = {
    id: `temp_${Date.now()}`,
    name,
    gender: form.value.gender,
    category: '通用',
    style: '自定义音色',
    visibility: 'private',
    provider: '自定义',
    userId: 'local_user',
    createdAt: new Date().toISOString(),
    uploadStatus: 'converting',
  }

  emit('confirmed', { voice: pendingVoice, formData: fd })
  emit('update:open', false)
}
</script>

<style scoped lang="less">
@import '../styles/variables.less';

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

.content { padding: 12px 12px; }

.tab-fade-enter-active,
.tab-fade-leave-active { transition: opacity 0.15s ease; }
.tab-fade-enter-from,
.tab-fade-leave-to { opacity: 0; }

.form-area {
  border-top: 1px solid @divider-color;
  padding: 10px 12px 6px;
}

.form-row {
  display: flex; align-items: center; gap: 8px;
  margin-bottom: 12px;
}

.form-gender { display: flex; gap: 6px; flex-shrink: 0; }

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

.slide-up-enter-active { animation: sl 0.2s cubic-bezier(0.34, 1.56, 0.64, 1); }
.slide-up-leave-active { transition: opacity 0.12s, transform 0.12s; }
.slide-up-enter-from { opacity: 0; transform: translateY(-6px); }
.slide-up-leave-to { opacity: 0; transform: translateY(-4px); }
@keyframes sl { from { opacity: 0; transform: translateY(-6px); } to { opacity: 1; transform: translateY(0); } }

@media (prefers-reduced-motion: reduce) {
  .tab-fade-enter-active, .tab-fade-leave-active,
  .slide-up-enter-active, .slide-up-leave-active { transition: none; }
  .slide-up-enter-from, .slide-up-leave-to { opacity: 1; transform: none; }
  .tab-fade-enter-from, .tab-fade-leave-to { opacity: 1; }
}
</style>
