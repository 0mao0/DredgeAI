<template>
  <div class="speech-draft-step">
    <SectionCard flush>
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
        <div class="speech-draft-step__header">
          <div class="speech-draft-step__header-left">
            <span class="speech-draft-step__date"><CalendarOutlined /> {{ dateText }}</span>
            <span class="speech-draft-step__badge">AI 已生成</span>
          </div>
          <span class="speech-draft-step__stat">{{ charCount }} 字 · 约 {{ minutes }} 分钟</span>
        </div>

        <div class="speech-draft-step__player">
          <SpeechPlayer ref="playerRef" :text="content" :meeting-id="meetingId" />
        </div>

        <div class="speech-draft-step__scroll">
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
        </div>

        <div class="speech-draft-step__footer">
          <div class="speech-draft-step__actions">
            <AppButton v-if="!editing" size="sm" variant="text" @click="onToggleEdit">
              编辑
            </AppButton>
            <div class="speech-draft-step__actions-right">
              <template v-if="editing">
                <a-config-provider :auto-insert-space-in-button="false">
                  <AppButton size="lg" @click="onCancelEdit">取消</AppButton>
                  <AppButton variant="primary" size="lg" :loading="loading" @click="onSave">保存</AppButton>
                </a-config-provider>
              </template>
              <template v-else>
                <AppButton variant="primary" size="lg" :loading="loading" @click="onConfirm">
                  立刻开会
                </AppButton>
              </template>
            </div>
          </div>
          <p v-if="noEvidenceNote" class="speech-draft-step__no-evidence">{{ noEvidenceNote }}</p>
        </div>
      </template>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { CalendarOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { SpeechDraftDto } from '@/types'
import SpeechPlayer from './SpeechPlayer.vue'

const props = defineProps<{
  draft: SpeechDraftDto | null
  loading: boolean
  date?: string
  meetingId?: string
}>()
const emit = defineEmits<{
  generate: []
  save: [content: string]
  confirm: []
}>()

const NO_EVIDENCE_NOTE = '本段依据无知识库证据'

const editing = ref(false)
const content = ref('')
const snapshot = ref<string | null>(null)
const playerRef = ref<InstanceType<typeof SpeechPlayer> | null>(null)

watch(
  () => props.draft,
  (d) => {
    if (d) content.value = d.content
  },
  { immediate: true },
)

const parsed = computed(() => {
  const lines: string[] = []
  let note = ''
  for (const raw of content.value.split('\n')) {
    const line = raw.trim()
    if (!line) continue
    if (line.includes(NO_EVIDENCE_NOTE)) {
      note = NO_EVIDENCE_NOTE
      const rest = line.split(NO_EVIDENCE_NOTE).join('').replace(/[()（）【】[\]]/g, '').trim()
      if (rest) lines.push(rest)
      continue
    }
    lines.push(line)
  }
  return { lines, note }
})
const paragraphs = computed(() => parsed.value.lines)
const noEvidenceNote = computed(() => parsed.value.note)
const charCount = computed(() => content.value.replace(/\s/g, '').length)
const minutes = computed(() => Math.max(1, Math.ceil(charCount.value / 4 / 60)))
const dateText = computed(() => props.date?.slice(0, 10) ?? '')

function onToggleEdit(): void {
  editing.value = !editing.value
  if (editing.value) snapshot.value = content.value
}

function onCancelEdit(): void {
  if (snapshot.value !== null) content.value = snapshot.value
  snapshot.value = null
  editing.value = false
}

function onSave(): void {
  emit('save', content.value)
  snapshot.value = null
  editing.value = false
}

function onConfirm(): void {
  // 点击“立刻开会”立即暂停试听，避免接口调用期间音频继续播放
  playerRef.value?.stop()
  emit('confirm')
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.speech-draft-step {
  height: clamp(420px, calc(100vh - 190px), 760px);
  min-height: 420px;

  :deep(.section-card) {
    height: 100%;
    display: flex;
    flex-direction: column;
  }
  :deep(.section-card-body) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }
}

.speech-draft-step__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-sm;
  padding: @spacing-lg 0 @spacing-base;
  border-bottom: 1px solid @divider-color;
  margin-bottom: @spacing-base;
}
.speech-draft-step__header-left {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  min-width: 0;
}
.speech-draft-step__date {
  font-size: @font-size-sm;
  color: @text-secondary;
  white-space: nowrap;
}
.speech-draft-step__badge {
  font-size: @font-size-xs;
  color: @brand-primary;
  background: color-mix(in srgb, var(--color-brand) 10%, transparent);
  padding: 2px @spacing-sm;
  border-radius: @radius-sm;
  font-weight: @font-weight-medium;
  white-space: nowrap;
}
.speech-draft-step__stat {
  font-size: @font-size-sm;
  color: @text-tertiary;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.speech-draft-step__player {
  margin-bottom: @spacing-base;
}

.speech-draft-step__scroll {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding-right: @spacing-xs;

  &::-webkit-scrollbar {
    width: 6px;
  }
  &::-webkit-scrollbar-thumb {
    background: color-mix(in srgb, var(--color-text-tertiary) 24%, transparent);
    border-radius: 999px;
  }
}

.speech-draft-step__loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: @spacing-md;
  padding: @spacing-2xl 0;
}
.speech-draft-step__generate {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
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

.speech-draft-step__card {
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

.speech-draft-step__footer {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: @spacing-sm;
  padding-top: @spacing-base;
  margin-top: @spacing-base;
  border-top: 1px solid @divider-color;
}
.speech-draft-step__actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-sm;
  width: 100%;

  .speech-draft-step__actions-right > * {
    min-width: 108px;
  }
}
.speech-draft-step__actions-right {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-left: auto;
}
.speech-draft-step__no-evidence {
  margin: 0;
  font-size: @font-size-xs;
  color: @text-tertiary;
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

@media (max-width: 520px) {
  .speech-draft-step {
    height: auto;
    min-height: 0;

    :deep(.section-card) {
      height: auto;
      display: block;
    }
    :deep(.section-card-body) {
      display: block;
    }
  }
  .speech-draft-step__scroll {
    overflow: visible;
    padding-right: 0;
  }
}
</style>
