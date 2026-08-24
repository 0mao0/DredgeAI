<template>
  <div class="player">
    <AudioPlayer :src="task.audioUrl">
      <template #extra>
        <a-tooltip title="下载音频">
          <AppButton
            v-if="task.status === '已完成' && task.audioUrl"
            :href="task.audioUrl"
            download="dubbing-audio.mp3"
            class="player__download"
          >
            <DownloadOutlined /> 下载
          </AppButton>
        </a-tooltip>
      </template>
    </AudioPlayer>
  </div>
</template>

<script setup lang="ts">
import { AppButton, AudioPlayer } from '@shared/web'
import { DownloadOutlined } from '@ant-design/icons-vue'
import type { DubbingTask } from '@/types'

defineProps<{
  task: DubbingTask
}>()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.player__download {
  margin-left: 0;
}
</style>
