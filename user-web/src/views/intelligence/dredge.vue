<template>
  <div class="dredge-page">
    <PageHeader
      title="疏浚情报"
      description="聚焦疏浚行业的科技与工程情报，由后台采集并结构化后发布"
    >
      <template #extra>
        <a-button size="small" @click="openInNewTab">
          <template #icon><ExportOutlined /></template>
          在新标签页打开
        </a-button>
      </template>
    </PageHeader>

    <div class="dredge-page__frame">
      <iframe
        class="dredge-page__iframe"
        :src="DREDGE_INTELLIGENCE_URL"
        title="疏浚情报"
        @load="handleLoad"
      />
      <div v-if="!loaded && !timedOut" class="dredge-page__loading">
        <a-spin size="large" />
        <p class="dredge-page__loading-text">正在加载疏浚情报…</p>
      </div>
      <a-alert
        v-if="timedOut"
        class="dredge-page__timeout"
        type="warning"
        show-icon
        message="加载超时"
        description="站点可能拒绝了内嵌访问或暂时不可用，请点击右上角「在新标签页打开」按钮访问。"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { ExportOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'

/** 疏浚情报站点的默认加载地址 */
const DREDGE_INTELLIGENCE_URL = 'http://124.221.238.70:8000/'

/** 内嵌加载超时（毫秒）：超过后提示用户改用新标签页打开 */
const LOAD_TIMEOUT = 8000

const loaded = ref(false)
const timedOut = ref(false)
let timer: ReturnType<typeof setTimeout> | null = null

function clearTimer(): void {
  if (timer) {
    clearTimeout(timer)
    timer = null
  }
}

function handleLoad(): void {
  loaded.value = true
  clearTimer()
}

function openInNewTab(): void {
  window.open(DREDGE_INTELLIGENCE_URL, '_blank', 'noopener')
}

onMounted(() => {
  timer = setTimeout(() => {
    if (!loaded.value) timedOut.value = true
  }, LOAD_TIMEOUT)
})

onBeforeUnmount(clearTimer)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.dredge-page {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  padding: @page-padding;
}

.dredge-page__frame {
  position: relative;
  flex: 1;
  min-height: 0;
  border-radius: @radius-lg;
  overflow: hidden;
  background: @card-bg;
}

.dredge-page__iframe {
  display: block;
  width: 100%;
  height: 100%;
  border: none;
}

.dredge-page__loading {
  position: absolute;
  inset: 0;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-md;
  background: @card-bg;
}

.dredge-page__loading-text {
  margin: 0;
  font-size: @font-size-sm;
  color: @text-secondary;
}

.dredge-page__timeout {
  position: absolute;
  top: @spacing-md;
  left: @spacing-md;
  right: @spacing-md;
  z-index: 1;
}
</style>
