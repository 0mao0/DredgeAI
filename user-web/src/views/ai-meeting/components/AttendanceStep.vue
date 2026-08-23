<template>
  <SectionCard title="现场点名" flush>
    <video v-if="stream" ref="videoRef" class="attendance-step__video" autoplay playsinline />
    <a-result v-else-if="error" status="warning" title="无法访问摄像头" :sub-title="error" />
    <div class="attendance-step__actions">
      <AppButton variant="primary" block :loading="loading" @click="onCapture">
        拍照识别（手动支架扫一圈，多拍几次自动去重）
      </AppButton>
      <div v-if="count !== null" class="attendance-step__count">
        本次照片识别 {{ list.length }} 人 · YOLO 目测人数 {{ count }}
      </div>
      <AppButton size="sm" block @click="enrollOpen = true">新人录入（拍身份证 + 人脸）</AppButton>
    </div>
    <a-table
      size="small"
      row-key="workerId"
      :columns="[
        { title: '姓名', dataIndex: 'name' },
        { title: '班组', dataIndex: 'team' },
        { title: '状态', dataIndex: 'status', width: 110 },
        { title: '置信度', dataIndex: 'confidence', width: 100 },
      ]"
      :data-source="list"
      :pagination="false"
    />
    <AppButton v-if="list.length" variant="primary" size="lg" block @click="emit('done')">
      点名完成，进入会议
    </AppButton>

    <WorkerEnrollDrawer v-model:open="enrollOpen" />
  </SectionCard>
</template>

<script setup lang="ts">
import { onMounted, onScopeDispose, ref } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { AttendanceItemDto } from '@/types'
import { useCamera } from '../composables/useCamera'
import WorkerEnrollDrawer from './WorkerEnrollDrawer.vue'

defineProps<{
  loading: boolean
  list: AttendanceItemDto[]
  count: number | null
}>()
const emit = defineEmits<{ capture: [photo: Blob], done: [] }>()

const videoRef = ref<HTMLVideoElement | null>(null)
const enrollOpen = ref(false)
const { stream, error, start, stop, capturePhoto } = useCamera()

onMounted(() => {
  void start()
})
onScopeDispose(() => stop())

async function onCapture(): Promise<void> {
  if (!videoRef.value || !stream.value) return
  const photo = await capturePhoto(videoRef.value)
  emit('capture', photo)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.attendance-step__video {
  width: 100%;
  border-radius: @radius-base;
}
.attendance-step__actions {
  margin: @spacing-md 0;
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;
}
.attendance-step__count {
  text-align: center;
  font-size: @font-size-sm;
  color: @text-secondary;
}
</style>
