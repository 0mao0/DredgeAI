<template>
  <SectionCard title="现场点名" flush>
    <video
      v-if="stream"
      ref="videoRef"
      class="attendance-step__video"
      :src-object="stream"
      autoplay
      playsinline
    />
    <div v-else-if="starting" class="attendance-step__camera-hint">正在启用摄像头…</div>
    <a-result v-else-if="error" status="warning" title="无法访问摄像头" :sub-title="error">
      <template #extra>
        <AppButton size="sm" @click="onRetryCamera">重新启用</AppButton>
      </template>
    </a-result>
    <a-alert
      v-if="stream"
      type="info"
      show-icon
      class="attendance-step__scan-tip"
      :message="scanning ? '正在自动识别…' : '摄像头已就绪'"
      :description="scanning ? `请旋转摄像头，覆盖所有在场人员（已识别 ${list.length} 人）` : '稍候将自动开始识别'"
    />
    <div v-if="count !== null" class="attendance-step__count">
      本次照片识别 {{ list.length }} 人 · YOLO 目测人数 {{ count }}
    </div>
    <DataTable
      :columns="columns"
      :data-source="list"
      row-key="workerId"
      :pagination="false"
      :card="false"
    />
    <AppButton variant="primary" size="lg" block @click="onDone">
      完成点名，进入会议
    </AppButton>
    <AppButton size="sm" block class="attendance-step__enroll" @click="enrollOpen = true">
      新人录入（拍身份证 + 人脸）
    </AppButton>

    <WorkerEnrollDrawer v-model:open="enrollOpen" />
  </SectionCard>
</template>

<script setup lang="ts">
import { nextTick, onMounted, onScopeDispose, ref, watch } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import type { AttendanceItemDto } from '@/types'
import { useCamera } from '../composables/useCamera'
import WorkerEnrollDrawer from './WorkerEnrollDrawer.vue'

defineProps<{
  loading: boolean
  list: AttendanceItemDto[]
  count: number | null
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
  while (scanning.value && token === scanToken) {
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
.attendance-step__scan-tip {
  margin: @spacing-md 0;
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
.attendance-step__enroll {
  margin-top: @spacing-md;
}
</style>
