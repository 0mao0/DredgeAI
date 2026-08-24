<template>
  <SectionCard title="现场点名" flush>
    <div v-if="stream" class="attendance-step__video-wrap">
      <video
        ref="videoRef"
        class="attendance-step__video"
        :src-object="stream"
        autoplay
        playsinline
        muted
      />
      <div class="attendance-step__scan-overlay">
        <div class="attendance-step__scan-title">
          <LoadingOutlined v-if="scanning" spin />
          <span v-if="scanning">正在自动识别… 已识别 {{ list.length }} 人</span>
          <span v-else>摄像头已就绪</span>
        </div>
        <div class="attendance-step__scan-guide">请旋转摄像头，覆盖所有在场人员</div>
      </div>
    </div>
    <div v-else-if="starting" class="attendance-step__camera-hint">正在启用摄像头…</div>
    <a-result v-else-if="error" status="warning" title="无法访问摄像头" :sub-title="error">
      <template #extra>
        <AppButton size="sm" @click="onRetryCamera">重新启用</AppButton>
      </template>
    </a-result>
    <div v-if="speechText" class="attendance-step__speech">
      <SpeechPlayer :text="speechText" auto-play />
    </div>
    <div class="attendance-step__actions">
      <AppButton variant="primary" :loading="loading" @click="onDone">
        完成点名，进入会议
      </AppButton>
      <AppButton @click="enrollOpen = true">新人录入</AppButton>
    </div>
    <div v-if="count !== null" class="attendance-step__count">
      已识别 {{ list.length }} 人 · YOLO 目测人数 {{ count }}
    </div>
    <DataTable
      :columns="columns"
      :data-source="list"
      row-key="workerId"
      :pagination="false"
      :card="false"
    />
    <WorkerEnrollDrawer v-model:open="enrollOpen" />
  </SectionCard>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onScopeDispose, ref, watch } from 'vue'
import { LoadingOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import type { AttendanceItemDto } from '@/types'
import { useCamera } from '../composables/useCamera'
import WorkerEnrollDrawer from './WorkerEnrollDrawer.vue'
import SpeechPlayer from './SpeechPlayer.vue'

defineProps<{
  loading: boolean
  list: AttendanceItemDto[]
  count: number | null
  speechText: string
}>()
const emit = defineEmits<{ capture: [photo: Blob], done: [] }>()

const columns: DataTableColumn[] = [
  { title: '姓名', dataIndex: 'name', key: 'name', width: 120, minWidth: 100, resizable: true },
  { title: '班组', dataIndex: 'team', key: 'team', width: 160, minWidth: 120, resizable: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110, minWidth: 90, resizable: true },
  { title: '置信度', dataIndex: 'confidence', key: 'confidence', width: 100, minWidth: 80, resizable: true },
]

const videoRef = ref<HTMLVideoElement | null>(null)
const enrollOpen = ref(false)
const scanning = ref(false)
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

.attendance-step__video {
  width: 100%;
  border-radius: @radius-base;
}
.attendance-step__video-wrap {
  position: relative;
  border-radius: @radius-base;
  overflow: hidden;
}
.attendance-step__scan-overlay {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  padding: @spacing-sm @spacing-md;
  background: color-mix(in srgb, #000 55%, transparent);
  color: #fff;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.attendance-step__scan-title {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
}
.attendance-step__scan-guide {
  font-size: @font-size-xs;
  opacity: 0.85;
}
.attendance-step__speech {
  margin-bottom: @spacing-md;
}
.attendance-step__actions {
  display: flex;
  gap: @spacing-sm;
  margin: @spacing-md 0;

  > * {
    flex: 1;
  }
}
.attendance-step__count {
  text-align: center;
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-bottom: @spacing-md;
}
.attendance-step__camera-hint {
  text-align: center;
  padding: @spacing-2xl 0;
  color: @text-secondary;
  background: @content-bg;
  border-radius: @radius-base;
}
</style>
