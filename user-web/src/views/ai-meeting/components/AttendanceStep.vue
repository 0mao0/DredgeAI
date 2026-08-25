<template>
  <SectionCard title="现场点名" flush class="attendance-step">
    <template v-if="count !== null" #extra>
      <span class="attendance-step__count">
        已识别 {{ list.length }} 人 · 目测 {{ count }} 人
      </span>
    </template>

    <div v-if="stream" class="attendance-step__video-wrap">
      <video
        ref="videoRef"
        class="attendance-step__video"
        :src-object="stream"
        autoplay
        playsinline
        muted
      />
      <div v-if="currentSegment" class="attendance-step__subtitle">{{ currentSegment }}</div>
      <div class="attendance-step__list-overlay">
        <div class="attendance-step__list-header">
          <span class="attendance-step__list-title">识别列表</span>
          <span class="attendance-step__list-scan">
            <LoadingOutlined v-if="scanning" spin />
            <template v-if="scanning">正在自动识别… 已识别 {{ list.length }} 人</template>
            <template v-else>摄像头已就绪</template>
          </span>
          <a-popover trigger="click" placement="bottomRight" :overlay-inner-style="{ padding: 0 }">
            <template #content>
              <div class="attendance-step__unknown-panel">
                <div class="attendance-step__unknown-panel-title">未识别人脸（待入库）</div>
                <div v-if="unrecognized.length === 0" class="attendance-step__unknown-empty">
                  暂未识别到未录入人脸
                </div>
                <div
                  v-for="(item, index) in unrecognized"
                  :key="`${item.workerId}-${index}`"
                  class="attendance-step__unknown-item"
                >
                  <span class="attendance-step__unknown-index">{{ index + 1 }}</span>
                  <span class="attendance-step__unknown-name">未识别人脸</span>
                  <span class="attendance-step__unknown-confidence">
                    {{ Math.round((item.confidence ?? 0) * 100) }}%
                  </span>
                </div>
              </div>
            </template>
            <AppButton size="sm" class="attendance-step__unknown-trigger">
              <UserOutlined />
              <span>未识别 {{ unrecognized.length }}</span>
            </AppButton>
          </a-popover>
        </div>
        <div v-if="list.length === 0" class="attendance-step__list-empty">等待识别…</div>
        <div v-else class="attendance-step__list-body">
          <div v-for="item in list" :key="item.workerId ?? item.name" class="attendance-step__person">
            <span class="attendance-step__person-dot" :class="`is-${item.status}`" />
            <span class="attendance-step__person-name">{{ personName(item) }}</span>
            <span class="attendance-step__person-team">{{ item.team }}</span>
          </div>
        </div>
      </div>
    </div>
    <div v-else-if="starting" class="attendance-step__camera-hint">正在启用摄像头…</div>
    <a-result v-else-if="error" status="warning" title="无法访问摄像头" :sub-title="error">
      <template #extra>
        <AppButton size="sm" @click="onRetryCamera">重新启用</AppButton>
      </template>
    </a-result>

    <div class="attendance-step__bottom">
      <div v-if="speechText" class="attendance-step__speech">
        <SpeechPlayer
          :text="speechText"
          auto-play
          play-cached-only
          :meeting-id="meetingId"
          @current-segment="currentSegment = $event"
        />
      </div>
      <div class="attendance-step__actions">
        <AppButton @click="enrollOpen = true">新人录入</AppButton>
        <AppButton variant="primary" @click="onDone">下一步</AppButton>
      </div>
    </div>

    <WorkerEnrollDrawer v-model:open="enrollOpen" />
  </SectionCard>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onScopeDispose, ref, watch } from 'vue'
import { LoadingOutlined, UserOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { AttendanceItemDto } from '@/types'
import { useCamera } from '../composables/useCamera'
import WorkerEnrollDrawer from './WorkerEnrollDrawer.vue'
import SpeechPlayer from './SpeechPlayer.vue'
import { displayAttendanceName } from '@/utils/attendanceName'

const props = defineProps<{
  loading: boolean
  list: AttendanceItemDto[]
  unrecognized: AttendanceItemDto[]
  count: number | null
  speechText: string
  meetingId?: string
}>()
const emit = defineEmits<{ capture: [photo: Blob], done: [] }>()

function personName(item: AttendanceItemDto): string {
  return displayAttendanceName(item, props.list)
}

const videoRef = ref<HTMLVideoElement | null>(null)
const enrollOpen = ref(false)
const scanning = ref(false)
const currentSegment = ref('')
let scanToken = 0
const { stream, error, starting, start, stop, capturePhoto } = useCamera()

onMounted(() => {
  void start()
})
onScopeDispose(() => {
  scanning.value = false
  scanToken++
  stop()
})

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
  // 等待视频画面就绪后自动拍照识别一次
  for (let i = 0; i < 20; i++) {
    if (videoRef.value && videoRef.value.videoWidth > 0) break
    await new Promise((r) => setTimeout(r, 250))
  }
  await startScanning()
})

async function startScanning(): Promise<void> {
  const token = ++scanToken
  scanning.value = true
  while (scanning.value) {
    if (token !== scanToken) break
    if (!videoRef.value || !stream.value || !videoRef.value.videoWidth) {
      await new Promise((r) => setTimeout(r, 1000))
      continue
    }
    try {
      const photo = await capturePhoto(videoRef.value)
      emit('capture', photo)
    } catch {
      // 单帧失败跳过，继续下一轮
    }
    await new Promise((r) => setTimeout(r, 2500))
  }
}

async function onRetryCamera(): Promise<void> {
  const ok = await start()
  if (ok) {
    await nextTick()
    await startScanning()
  }
}

function onDone(): void {
  scanning.value = false
  scanToken++
  emit('done')
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.attendance-step {
  height: clamp(420px, calc(100vh - 190px), 760px);
  min-height: 420px;
  display: flex;
  flex-direction: column;

  :deep(.section-card-header) {
    flex: none;
  }
  :deep(.section-card-body) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }
}
.attendance-step__count {
  font-size: @font-size-sm;
  color: @text-secondary;
  white-space: nowrap;
}

.attendance-step__video-wrap {
  position: relative;
  flex: 1;
  min-height: 0;
  border-radius: @radius-base;
  overflow: hidden;
}
.attendance-step__video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
.attendance-step__subtitle {
  position: absolute;
  top: @spacing-sm;
  left: @spacing-sm;
  right: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border-radius: @radius-sm;
  background: color-mix(in srgb, #000 55%, transparent);
  color: #fff;
  font-size: @font-size-2xl;
  line-height: 1.35;
  text-align: center;
  white-space: nowrap;
  overflow: hidden;
  z-index: 2;
  pointer-events: none;
  box-shadow: @shadow-sm;
}
.attendance-step__unknown-panel {
  min-width: 220px;
  max-height: 260px;
  overflow-y: auto;
}
.attendance-step__list-header {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border-bottom: 1px solid @divider-color;
}
.attendance-step__unknown-panel-title {
  padding: @spacing-sm @spacing-md;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
  border-bottom: 1px solid @divider-color;
}
.attendance-step__list-scan {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  font-size: @font-size-xs;
  color: @text-secondary;
  font-weight: @font-weight-medium;
}
.attendance-step__unknown-trigger {
  flex: none;
  height: 24px;
  padding: 0 @spacing-sm;
  font-size: @font-size-xs;
  background: color-mix(in srgb, var(--color-card-bg) 65%, transparent);
  border-color: @border-color;
  color: @text-secondary;

  &:hover,
  &:focus {
    background: var(--color-card-bg);
    border-color: @text-tertiary;
    color: @text-primary;
  }
}
.attendance-step__unknown-item {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  font-size: @font-size-sm;
  color: @text-secondary;

  &:hover {
    background: @surface-hover;
  }
}
.attendance-step__unknown-index {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: none;
  font-size: @font-size-xs;
  font-weight: @font-weight-medium;
  color: @warning;
  background: color-mix(in srgb, var(--color-warning) 15%, transparent);
}
.attendance-step__unknown-name {
  min-width: 0;
}
.attendance-step__unknown-confidence {
  margin-left: auto;
  font-size: @font-size-xs;
  color: @text-tertiary;
  font-variant-numeric: tabular-nums;
}
.attendance-step__unknown-empty {
  padding: @spacing-lg @spacing-md;
  text-align: center;
  font-size: @font-size-sm;
  color: @text-tertiary;
}
.attendance-step__list-overlay {
  position: absolute;
  left: @spacing-sm;
  right: @spacing-sm;
  bottom: @spacing-sm;
  max-height: min(40%, 220px);
  display: flex;
  flex-direction: column;
  border-radius: @radius-base;
  background: var(--glass-fill);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  box-shadow: @shadow-md;
  overflow: hidden;
}
.attendance-step__list-title {
  flex: none;
  font-size: @font-size-xs;
  font-weight: @font-weight-medium;
  color: @text-secondary;
}
.attendance-step__list-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-wrap: wrap;
  gap: @spacing-xs;
  padding: @spacing-sm @spacing-md;
}
.attendance-step__person {
  display: inline-flex;
  align-items: center;
  gap: @spacing-xs;
  padding: 2px @spacing-sm;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-card-bg) 45%, transparent);
  font-size: @font-size-xs;
  white-space: nowrap;
}
.attendance-step__person-name {
  color: @text-primary;
  font-weight: @font-weight-medium;
}
.attendance-step__person-team {
  color: @text-tertiary;
}
.attendance-step__person-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex: none;
  background: @text-tertiary;

  &.is-present {
    background: @success;
  }
  &.is-late {
    background: @warning;
  }
  &.is-absent {
    background: @danger;
  }
}
.attendance-step__list-empty {
  padding: @spacing-sm @spacing-md;
  font-size: @font-size-xs;
  color: @text-tertiary;
}

.attendance-step__bottom {
  flex: none;
  margin-top: @spacing-md;
}
.attendance-step__speech {
  margin-bottom: @spacing-md;
}
.attendance-step__actions {
  display: flex;
  gap: @spacing-sm;

  > * {
    flex: 1;
  }
}
.attendance-step__camera-hint {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: @text-secondary;
  background: @content-bg;
  border-radius: @radius-base;
}
</style>
