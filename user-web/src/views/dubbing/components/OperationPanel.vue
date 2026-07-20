<template>
  <div class="op-panel">
    <!-- 左侧：语音列表 -->
    <section class="op-panel__left">
      <SectionCard title="选择声音" class="op-panel__left-card" :flush="true">
        <div class="op-panel__left-body">
          <VoicePicker
            :voices="voices"
            :loading="voicesLoading"
            :model-value="voiceId"
            @update:model-value="(v: string) => emit('update:voiceId', v)"
          />
        </div>
      </SectionCard>
    </section>

    <!-- 右侧：输入 + 播放 -->
    <section class="op-panel__right">
      <SectionCard class="op-panel__input-card">
        <template #title>
          <span class="op-panel__title-with-voice">
            输入文本
            <a-tag color="blue" class="op-panel__current-voice-tag">
              <CustomerServiceOutlined /> {{ selectedVoiceName }}
            </a-tag>
          </span>
        </template>
        <template #extra>
          <span class="op-panel__charcount" :class="{ 'op-panel__charcount--warn': text.length > 4500 }">
            {{ text.length }}/5000
          </span>
        </template>
        <DubbingInputPanel
          :speed="speed"
          :generating="generating"
          :text="text"
          @update:text="(v: string) => emit('update:text', v)"
          @update:speed="(v: number) => emit('update:speed', v)"
          @generate="emit('generate', $event)"
        />
      </SectionCard>

      <SectionCard title="生成结果" class="op-panel__result-card">
        <transition name="fade" mode="out-in">
          <!-- 生成中：步骤进度 -->
          <div v-if="generating || producingTask" :key="'producing'" class="result-producing">
            <div class="producing-steps">
              <div
                v-for="(step, i) in steps"
                :key="step.key"
                class="producing-step"
                :class="stepClass(i)"
              >
                <span class="producing-step__dot">
                  <CheckOutlined v-if="stepState(i) === 'done'" />
                  <LoadingOutlined v-else-if="stepState(i) === 'active'" spin />
                  <span v-else>{{ i + 1 }}</span>
                </span>
                <span class="producing-step__label">{{ step.label }}</span>
              </div>
            </div>
            <div class="producing-wave" aria-hidden="true">
              <span v-for="n in 28" :key="n" class="producing-wave__bar" :style="{ animationDelay: (n * 60) + 'ms' }" />
            </div>
            <p class="producing-tip">正在合成语音，预计需要几秒钟，请稍候…</p>
          </div>

          <!-- 已完成：播放器 -->
          <div v-else-if="currentTask && currentTask.status === '已完成'" :key="currentTask.id" class="result-area">
            <a-tag color="green" class="result-status">
              <CheckCircleFilled /> 合成完成
            </a-tag>
            <DubbingPlayer :task="currentTask" />
          </div>

          <!-- 失败 -->
          <div v-else-if="currentTask && currentTask.status === '已失败'" :key="'failed'" class="result-area">
            <a-result status="error" title="生成失败" sub-title="请检查文本内容后重试">
              <template #extra><a-button type="primary" @click="emit('reset')">重新生成</a-button></template>
            </a-result>
          </div>

          <!-- 空态 -->
          <div v-else key="empty" class="result-empty">
            <CustomerServiceOutlined class="result-empty__icon" />
            <p class="result-empty__title">还没有配音结果</p>
            <p class="result-empty__desc">在左侧选择音色并输入文本，点击「开始生成」即可创建你的第一段配音。</p>
          </div>
        </transition>
      </SectionCard>
    </section>
  </div>
</template>

<script setup lang="ts">
import {
  CheckOutlined,
  LoadingOutlined,
  CheckCircleFilled,
  CustomerServiceOutlined,
} from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import VoicePicker from './VoicePicker.vue'
import DubbingInputPanel from './DubbingInputPanel.vue'
import DubbingPlayer from './DubbingPlayer.vue'
import type { VoiceItem, DubbingTask } from '@/types'

const props = defineProps<{
  voices: VoiceItem[]
  voicesLoading: boolean
  voiceId: string
  selectedVoiceName: string
  text: string
  speed: number
  generating: boolean
  producingTask: boolean
  activeStep: number
  currentTask: DubbingTask | null
}>()

const emit = defineEmits<{
  'update:voiceId': [value: string]
  'update:text': [value: string]
  'update:speed': [value: number]
  generate: [text: string]
  reset: []
}>()

const steps = [
  { key: 'submit', label: '提交任务' },
  { key: 'produce', label: '合成语音' },
  { key: 'done', label: '完成' },
]

function stepState(i: number): 'done' | 'active' | 'todo' {
  if (i < props.activeStep) return 'done'
  if (i === props.activeStep) return 'active'
  return 'todo'
}
function stepClass(i: number): string {
  return `producing-step--${stepState(i)}`
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.op-panel {
  height: 100%;
  display: flex;
  gap: @spacing-xl;
  min-height: 0;

  :deep(.section-card-header) {
    padding: @spacing-lg @spacing-xl @spacing-sm;
  }
  :deep(.section-card-body) {
    padding-top: @spacing-md;
  }
  :deep(.ant-tabs-nav) {
    margin-bottom: @spacing-sm;
  }
  :deep(.ant-tabs-tab) {
    padding: 6px 10px;
    margin: 0 2px 0 0 !important;
  }

  &__left {
    width: 30%;
    min-width: 260px;
    max-width: 380px;
    display: flex;
    flex-direction: column;
    min-height: 0;
  }
  &__left-card {
    height: 100%;
    display: flex;
    flex-direction: column;
    min-height: 0;
    :deep(.section-card-body) {
      flex: 1;
      min-height: 0;
      overflow: hidden;
      padding: 0;
    }
  }
  &__left-body {
    height: 100%;
    min-height: 0;
    overflow-y: auto;
    padding: @spacing-md @spacing-xl @spacing-xl;
  }
  &__right {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: @spacing-xl;
    min-height: 0;
    overflow: hidden;
  }
  &__input-card {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    :deep(.section-card-body) {
      flex: 1;
      min-height: 0;
      display: flex;
      flex-direction: column;
    }
  }
  &__result-card {
    flex-shrink: 0;
    max-height: 42%;
    overflow: auto;
  }
  &__title-with-voice {
    display: inline-flex;
    align-items: center;
    gap: @spacing-sm;
  }
  &__current-voice-tag { font-size: @font-size-sm; margin-right: 0; }
  &__charcount {
    font-size: @font-size-xs;
    color: @text-tertiary;
    font-variant-numeric: tabular-nums;
    transition: color 0.2s ease;
  }
  &__charcount--warn { color: @warning; font-weight: @font-weight-medium; }
}

.result-area {
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  animation: fade-up 0.3s ease;
}
.result-status {
  align-self: flex-start;
  font-weight: @font-weight-medium;
}

.result-empty {
  min-height: 160px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: @spacing-lg 0;
  &__icon { font-size: 40px; color: @text-tertiary; margin-bottom: @spacing-sm; }
  &__title { font-size: @font-size-base; color: @text-primary; font-weight: @font-weight-medium; margin-bottom: @spacing-xs; }
  &__desc { font-size: @font-size-sm; color: @text-tertiary; max-width: 280px; line-height: 1.6; margin: 0; }
}

.result-producing {
  min-height: 160px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-lg;
  padding: @spacing-md 0;
}
.producing-steps {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
}
.producing-step {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  color: @text-tertiary;
  transition: color 0.3s ease;
  &__dot {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: @font-size-xs;
    border: 1.5px solid @border-color;
    transition: all 0.3s ease;
  }
  &__label { font-size: @font-size-sm; }
  & + &::before {
    content: '';
    width: 28px;
    height: 1.5px;
    background: @border-color;
    margin: 0 @spacing-xs;
  }
}
.producing-step--active {
  color: @brand-primary;
  .producing-step__dot {
    border-color: @brand-primary;
    color: @brand-primary;
    box-shadow: 0 0 0 3px color-mix(in srgb, @brand-primary 18%, transparent);
  }
}
.producing-step--done {
  color: @success;
  .producing-step__dot {
    border-color: @success;
    background: @success;
    color: #fff;
  }
}

.producing-wave {
  display: flex;
  align-items: center;
  gap: 3px;
  height: 40px;
  &__bar {
    width: 4px;
    height: 100%;
    border-radius: 2px;
    background: linear-gradient(@brand-primary, color-mix(in srgb, @brand-primary 40%, transparent));
    transform-origin: center;
    animation: wave 1s ease-in-out infinite;
  }
}
.producing-tip { font-size: @font-size-sm; color: @text-secondary; margin: 0; }

@keyframes wave {
  0%, 100% { transform: scaleY(0.3); }
  50% { transform: scaleY(1); }
}
@keyframes fade-up {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

.fade-enter-active, .fade-leave-active { transition: opacity 0.25s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

@media (max-width: 991px) {
  .op-panel {
    flex-direction: column;
    overflow-y: auto;
    &__left { width: 100%; min-width: 0; height: 420px; }
    &__right { min-height: 520px; }
  }
}

@media (prefers-reduced-motion: reduce) {
  .producing-wave__bar { animation: none; height: 60%; }
  .result-area { animation: none; }
  .fade-enter-active, .fade-leave-active { transition: none; }
}
</style>
