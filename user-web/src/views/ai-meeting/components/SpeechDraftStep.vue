<template>
  <div class="speech-draft-step">
    <SectionCard flush>
      <div v-if="!draft && !streaming" class="speech-draft-step__generate">
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
            <span v-if="streaming" class="speech-draft-step__badge is-streaming">AI 生成中…</span>
            <span v-else class="speech-draft-step__badge">AI 已生成</span>
          </div>
          <span class="speech-draft-step__stat">{{ charCount }} 字 · 约 {{ minutes }} 分钟</span>
        </div>

        <div v-if="!streaming" class="speech-draft-step__player">
          <SpeechPlayer ref="playerRef" :text="content" :meeting-id="meetingId" />
        </div>

        <div class="speech-draft-step__scroll">
          <div class="speech-draft-step__card">
            <template v-if="!editing">
              <!-- 生成中：管线进度展开为「思考过程」步骤列表，每步显示耗时（进行中每 0.1s 动态刷新） -->
              <div v-if="showSteps" class="speech-draft-step__steps">
                <div
                  v-for="(step, index) in stepViews"
                  :key="step.key"
                  class="speech-draft-step__step"
                  :class="{ 'is-active': index === spinnerIndex }"
                >
                  <LoadingOutlined v-if="index === spinnerIndex" spin class="speech-draft-step__step-icon" />
                  <CheckCircleFilled v-else class="speech-draft-step__step-icon is-done" />
                  <span>{{ step.label }}</span>
                  <span class="speech-draft-step__step-dur">{{ step.dur }}s</span>
                </div>
              </div>
              <!-- 生成结束：步骤收起为「生成过程」摘要行，点击展开回看各步耗时 -->
              <div v-else-if="visibleSteps.length > 0" class="speech-draft-step__runmeta">
                <button
                  type="button"
                  class="speech-draft-step__runmeta-toggle"
                  @click="toggleStepsExpanded"
                >
                  <span class="speech-draft-step__runmeta-caret" aria-hidden="true">{{ stepsExpanded ? '▾' : '▸' }}</span>
                  <span>生成过程 · 共 {{ runTotalText }}</span>
                  <span class="speech-draft-step__runmeta-count">{{ visibleSteps.length }} 步</span>
                  <span class="speech-draft-step__runmeta-hint">{{ stepsExpanded ? '收起' : '展开' }}</span>
                </button>
                <div v-if="stepsExpanded" class="speech-draft-step__steps is-archived">
                  <div v-for="step in stepViews" :key="step.key" class="speech-draft-step__step">
                    <CheckCircleFilled class="speech-draft-step__step-icon is-done" />
                    <span>{{ step.label }}</span>
                    <span class="speech-draft-step__step-dur">{{ step.dur }}s</span>
                  </div>
                </div>
              </div>
              <p
                v-for="(para, index) in paragraphs"
                :key="index"
                class="speech-draft-step__para"
                :class="{ 'is-lead': index === 0 }"
              >
                {{ para }}
              </p>
              <p v-if="streaming && paragraphs.length === 0 && !showSteps" class="speech-draft-step__typing-hint">
                正在组织语言…
              </p>
              <span v-if="streaming" class="speech-draft-step__cursor" />
            </template>
            <a-textarea
              v-else
              v-model:value="content"
              :rows="14"
              class="speech-draft-step__editor"
            />
          </div>
        </div>

        <div v-if="!streaming" class="speech-draft-step__footer">
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
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { CalendarOutlined, CheckCircleFilled, LoadingOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { SpeechDraftDto } from '@/types'
import SpeechPlayer from './SpeechPlayer.vue'
import { splitDraftAnnotations } from '@/utils/speechText'

const props = defineProps<{
  draft: SpeechDraftDto | null
  loading: boolean
  /** 流式生成中（草稿尚未落库，直接渲染 streamingText） */
  streaming?: boolean
  streamingText?: string
  /** 流式生成中的管线进度步骤（SSE status，at 为到达时刻 ms）；每步耗时按相邻到达时刻差值计算 */
  streamStatuses?: { key: string, label: string, at: number }[]
  date?: string
  meetingId?: string
}>()
const emit = defineEmits<{
  generate: []
  save: [content: string]
  confirm: []
}>()

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

const displayText = computed(() => (props.streaming && !props.draft ? props.streamingText ?? '' : content.value))
const parsed = computed(() => {
  // 标注句单独交给界面提示（见 noEvidenceNote），正文行用于渲染
  const { text, notes } = splitDraftAnnotations(displayText.value)
  return { lines: text ? text.split('\n') : [], note: notes[0] ?? '' }
})
const paragraphs = computed(() => parsed.value.lines)
const visibleSteps = computed(() => props.streamStatuses ?? [])
/** 正文是否已开始流出（决定还有没有「进行中」步骤） */
const textStarted = computed(() => (props.streamingText?.length ?? 0) > 0)
/** 生成结束后「生成过程」是否展开（默认折叠为摘要行；新一轮生成自动收起） */
const stepsExpanded = ref(false)
function toggleStepsExpanded(): void {
  stepsExpanded.value = !stepsExpanded.value
}
/** 整轮生成耗时：首步到达 → 末步（首个字到达）时刻差 */
const runTotalText = computed(() => {
  const steps = visibleSteps.value
  if (steps.length < 2) return '—'
  return `${((steps[steps.length - 1].at - steps[0].at) / 1000).toFixed(1)}s`
})
/** 正文开始流出后不再有「进行中」步骤（最后一步由「首个字到达」事件定格 TTFT） */
const spinnerIndex = computed(() => (textStarted.value ? -1 : visibleSteps.value.length - 1))
/** 流式期间持续展示步骤列表（正文流出后定格耗时、保留在正文上方） */
const showSteps = computed(() => Boolean(props.streaming) && visibleSteps.value.length > 0)

// 进行中步骤的耗时靠本地时钟每 100ms 推进；停止流式即停表，避免空转
const nowTick = ref(Date.now())
let tickTimer: ReturnType<typeof setInterval> | null = null
watch(
  () => props.streaming,
  (on) => {
    if (on) {
      nowTick.value = Date.now()
      stepsExpanded.value = false
      if (!tickTimer) tickTimer = setInterval(() => { nowTick.value = Date.now() }, 100)
    } else if (tickTimer) {
      clearInterval(tickTimer)
      tickTimer = null
    }
  },
  { immediate: true },
)
onBeforeUnmount(() => {
  if (tickTimer) clearInterval(tickTimer)
  tickTimer = null
})

// 步骤 i 的耗时 = 下一步到达时刻 − 本步到达时刻；进行中的末步用本地时钟实时推进
const stepViews = computed(() =>
  visibleSteps.value.map((step, index) => {
    const next = visibleSteps.value[index + 1]
    // 正文流出后末步按其自身到达时刻定格（其后即「首个字到达」事件，耗时 0.0s）
    const end = next ? next.at : index === spinnerIndex.value ? nowTick.value : step.at
    return { ...step, dur: Math.max(0, (end - step.at) / 1000).toFixed(1) }
  }),
)
const noEvidenceNote = computed(() => parsed.value.note)
const charCount = computed(() => displayText.value.replace(/\s/g, '').length)
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

  &.is-streaming {
    animation: speech-draft-pulse 1.6s infinite ease-in-out;
  }
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
.speech-draft-step__steps {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
  margin-bottom: @spacing-md;
}
.speech-draft-step__step {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  font-size: @font-size-sm;
  color: @text-tertiary;

  &.is-active {
    color: @text-primary;
  }
}
.speech-draft-step__step-icon {
  font-size: @font-size-sm;

  &.is-done {
    color: @success;
  }
}
.speech-draft-step__step-dur {
  color: @text-tertiary;
  font-size: @font-size-xs;
  font-variant-numeric: tabular-nums;
}

.speech-draft-step__runmeta {
  margin-bottom: @spacing-md;
}
.speech-draft-step__runmeta-toggle {
  display: inline-flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-xs @spacing-base;
  border: 1px solid @divider-color;
  border-radius: @radius-sm;
  background: color-mix(in srgb, var(--color-text-tertiary) 6%, transparent);
  cursor: pointer;
  font-size: @font-size-sm;
  color: @text-secondary;

  &:hover {
    color: @brand-primary;
    border-color: @brand-primary;
  }
}
.speech-draft-step__runmeta-caret {
  font-size: @font-size-sm;
  color: @text-secondary;
}
.speech-draft-step__runmeta-count {
  font-size: @font-size-xs;
  color: @text-tertiary;
}
.speech-draft-step__runmeta-hint {
  font-size: @font-size-xs;
  color: @brand-primary;
}
.speech-draft-step__steps.is-archived {
  margin-top: @spacing-sm;
  margin-bottom: 0;
  padding-left: @spacing-lg;
}
.speech-draft-step__typing-hint {
  margin: 0;
  font-size: @font-size-sm;
  color: @text-tertiary;
}
.speech-draft-step__cursor {
  display: inline-block;
  width: 2px;
  height: 1em;
  vertical-align: -0.15em;
  margin-left: 2px;
  background: @brand-primary;
  animation: speech-draft-cursor 0.8s infinite steps(1);
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

@keyframes speech-draft-pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.55;
  }
}

@keyframes speech-draft-cursor {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0;
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
