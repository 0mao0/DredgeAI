<template>
  <div class="speech-draft-step">
    <div v-if="!draft" class="speech-draft-step__generate">
      <div v-if="loading" class="speech-draft-step__loading">
        <div class="speech-draft-step__spinner">
          <span class="speech-draft-step__dot" />
          <span class="speech-draft-step__dot" />
          <span class="speech-draft-step__dot" />
        </div>
        <div class="speech-draft-step__loading-title">AI生成晨会稿中...</div>
        <div class="speech-draft-step__loading-sub">正在结合今日计划与知识库组织语言，请稍候</div>
      </div>
      <AppButton v-else variant="primary" block @click="emit('generate')">生成晨会稿</AppButton>
    </div>

    <template v-else>
      <div class="speech-draft-step__meta">
        <span class="speech-draft-step__date"><CalendarOutlined /> {{ dateText }}</span>
        <span class="speech-draft-step__badge">AI 已生成</span>
      </div>

      <div class="speech-draft-step__card">
        <template v-if="!editing">
          <p
            v-for="(para, index) in paragraphs"
            :key="index"
            class="speech-draft-step__para"
            :class="{ 'is-lead': index === 0 }"
          >
            {{ para }}
          </p>
        </template>
        <a-textarea
          v-else
          v-model:value="content"
          :rows="14"
          class="speech-draft-step__editor"
        />
      </div>

      <div class="speech-draft-step__toolbar">
        <AppButton size="sm" :loading="audioLoading" @click="onTogglePlay">
          <SoundOutlined /> {{ playing ? '停止试听' : '试听语音' }}
        </AppButton>
        <AppButton size="sm" variant="text" @click="onToggleEdit">
          {{ editing ? '取消编辑' : '编辑' }}
        </AppButton>
        <AppButton size="sm" variant="text" :disabled="!editing" @click="onSave">
          保存
        </AppButton>
        <span class="speech-draft-step__stat">{{ charCount }} 字 · 约 {{ minutes }} 分钟</span>
      </div>

      <AppButton variant="primary" size="lg" block :loading="loading" @click="emit('confirm')">
        确认并开始点名
      </AppButton>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CalendarOutlined, SoundOutlined } from '@ant-design/icons-vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { SpeechDraftDto } from '@/types'

const props = defineProps<{
  draft: SpeechDraftDto | null
  loading: boolean
  playing: boolean
  audioLoading: boolean
  date?: string
}>()
const emit = defineEmits<{
  generate: []
  save: [content: string]
  confirm: []
  playAudio: []
  stopAudio: []
}>()

const editing = ref(false)
const content = ref('')

watch(
  () => props.draft,
  (d) => {
    if (d) content.value = d.content
  },
  { immediate: true },
)

const paragraphs = computed(() =>
  content.value
    .split('\n')
    .map((s) => s.trim())
    .filter(Boolean),
)
const charCount = computed(() => content.value.replace(/\s/g, '').length)
const minutes = computed(() => Math.max(1, Math.ceil(charCount.value / 4 / 60)))
const dateText = computed(() => props.date?.slice(0, 10) ?? '')

function onTogglePlay(): void {
  if (props.playing) {
    emit('stopAudio')
  } else {
    emit('playAudio')
  }
}

function onToggleEdit(): void {
  editing.value = !editing.value
}

function onSave(): void {
  emit('save', content.value)
  editing.value = false
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.speech-draft-step__loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-md;
  padding: @spacing-2xl 0;
}
.speech-draft-step__spinner {
  display: flex;
  gap: @spacing-sm;
}
.speech-draft-step__dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: @brand-primary;
  animation: speech-draft-bounce 1.2s infinite ease-in-out;

  &:nth-child(2) {
    animation-delay: 0.15s;
  }
  &:nth-child(3) {
    animation-delay: 0.3s;
  }
}
.speech-draft-step__loading-title {
  font-size: @font-size-lg;
  color: @text-primary;
  font-weight: @font-weight-medium;
}
.speech-draft-step__loading-sub {
  font-size: @font-size-sm;
  color: @text-tertiary;
}

.speech-draft-step__meta {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-md;
}
.speech-draft-step__date {
  font-size: @font-size-sm;
  color: @text-secondary;
}
.speech-draft-step__badge {
  font-size: @font-size-xs;
  color: @brand-primary;
  background: color-mix(in srgb, var(--color-brand) 10%, transparent);
  padding: 2px @spacing-sm;
  border-radius: @radius-sm;
  font-weight: @font-weight-medium;
}

.speech-draft-step__card {
  background: @content-bg;
  border-radius: @radius-xl;
  padding: @spacing-xl @spacing-lg;
  margin-bottom: @spacing-md;
}
.speech-draft-step__para {
  margin: 0 0 @spacing-base;
  line-height: 1.8;
  font-size: @font-size-base;
  color: @text-primary;
  text-wrap: pretty;

  &:last-child {
    margin-bottom: 0;
  }
  &.is-lead {
    font-size: @font-size-lg;
    font-weight: @font-weight-semibold;
    color: @text-primary;
  }
}
.speech-draft-step__editor {
  font-size: @font-size-base;
  line-height: 1.8;
}

.speech-draft-step__toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: @spacing-sm;
  margin-bottom: @spacing-lg;

  :deep(.app-btn) {
    display: inline-flex;
    align-items: center;
  }
}
.speech-draft-step__stat {
  margin-left: auto;
  font-size: @font-size-sm;
  color: @text-tertiary;
  font-variant-numeric: tabular-nums;
}

@keyframes speech-draft-bounce {
  0%, 80%, 100% {
    transform: translateY(0);
    opacity: 0.45;
  }
  40% {
    transform: translateY(-10px);
    opacity: 1;
  }
}
</style>
