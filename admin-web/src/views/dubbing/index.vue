<template>
  <div class="dubbing-admin">
    <PageHeader title="AI 配音" />
    <a-tabs v-model:active-key="activeTab" class="dubbing-tabs">
      <a-tab-pane key="stats" tab="使用统计">
        <UsageMetrics :summary="summary" :loading="loadingSummary" />
        <UsageCharts :tasks="tasks" :time-series="timeSeries" :loading="loadingSummary" @range-change="handleRangeChange" />
      </a-tab-pane>
      <a-tab-pane key="history" tab="历史记录">
        <AdminDubbingFilters :loading="loading" @search="handleSearch" />
        <AdminHistoryTable :tasks="tasks" :loading="loading" @play="handlePlay" @delete="handleDelete" />
      </a-tab-pane>
      <a-tab-pane key="voice" tab="音色管理">
        <AdminVoiceManager
          :voices="voices"
          :loading="loadingVoices"
          @search="handleVoiceSearch"
          @create="handleVoiceCreate"
          @delete="handleVoiceDelete"
        />
      </a-tab-pane>
      <a-tab-pane key="permissions" tab="权限配置">
        <PermissionPanel />
      </a-tab-pane>
    </a-tabs>

    <a-modal v-model:visible="playerVisible" title="播放配音" :footer="null" width="480px" destroy-on-close>
      <div v-if="currentTask" class="player-wrap">
        <p class="player-text">{{ currentTask.text }}</p>
        <audio :src="currentTask.audioUrl" controls autoplay class="player-audio" />
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import AdminDubbingFilters from './components/AdminDubbingFilters.vue'
import AdminHistoryTable from './components/AdminHistoryTable.vue'
import AdminVoiceManager from './components/AdminVoiceManager.vue'
import UsageMetrics from './components/UsageMetrics.vue'
import UsageCharts from './components/UsageCharts.vue'
import PermissionPanel from './components/PermissionPanel.vue'
import { getAdminDubbingTasks, deleteAdminDubbingTask, getAdminDubbingUsageSummary, getAdminDubbingUsageTimeseries, getAdminVoices, createAdminVoice, deleteAdminVoice } from '@/api/modules/dubbing'
import type { DubbingTask, DubbingUsageSummary, DubbingUsageTimeSeries, VoiceItem } from '@/types'

const activeTab = ref('stats')
const tasks = ref<DubbingTask[]>([])
const loading = ref(false)
const summary = ref<DubbingUsageSummary | null>(null)
const timeSeries = ref<DubbingUsageTimeSeries | null>(null)
const loadingSummary = ref(false)
const currentFilters = ref({ keyword: '', status: '', deletedOnly: false, dateRange: ['', ''] as [string, string] })
const currentTask = ref<DubbingTask | null>(null)
const playerVisible = ref(false)
const voices = ref<VoiceItem[]>([])
const loadingVoices = ref(false)
const voiceFilters = ref({ keyword: '', deletedOnly: false })

async function fetchTasks(filters?: typeof currentFilters.value): Promise<void> {
  loading.value = true
  try {
    const params: Record<string, string | number> = {}
    if (filters?.keyword) params.keyword = filters.keyword
    if (filters?.status) params.status = filters.status
    if (filters?.deletedOnly) params.deletedOnly = 1
    const res = await getAdminDubbingTasks(Object.keys(params).length ? params : undefined)
    tasks.value = res.items
  } catch {
    message.error('加载配音记录失败')
  } finally {
    loading.value = false
  }
}

async function fetchUsage(range = '30d'): Promise<void> {
  loadingSummary.value = true
  try {
    const [s, ts] = await Promise.all([
      getAdminDubbingUsageSummary(),
      getAdminDubbingUsageTimeseries(range),
    ])
    summary.value = s
    timeSeries.value = ts
  } catch {
    message.error('加载使用统计失败')
  } finally {
    loadingSummary.value = false
  }
}

function handleSearch(filters: typeof currentFilters.value): void {
  currentFilters.value = filters
  fetchTasks(filters)
}

function handlePlay(task: DubbingTask): void {
  currentTask.value = task
  playerVisible.value = true
}

async function handleDelete(id: string): Promise<void> {
  try {
    await deleteAdminDubbingTask(id)
    message.success('已删除')
    fetchTasks(currentFilters.value)
  } catch {
    message.error('删除失败，请重试')
  }
}

function handleRangeChange(range: string): void {
  fetchUsage(range)
}

async function fetchVoices(): Promise<void> {
  loadingVoices.value = true
  try {
    const params: Record<string, string | number> = {}
    if (voiceFilters.value.keyword) params.keyword = voiceFilters.value.keyword
    if (voiceFilters.value.deletedOnly) params.deletedOnly = 1
    const res = await getAdminVoices(Object.keys(params).length ? params : undefined)
    voices.value = res as VoiceItem[]
  } catch {
    message.error('加载音色列表失败')
  } finally {
    loadingVoices.value = false
  }
}

function handleVoiceSearch(params: { keyword: string, deletedOnly: boolean }): void {
  voiceFilters.value = params
  fetchVoices()
}

async function handleVoiceCreate(formData: FormData): Promise<void> {
  try {
    await createAdminVoice(formData)
    message.success('公有音色已添加')
    fetchVoices()
  } catch {
    message.error('添加失败')
  }
}

async function handleVoiceDelete(id: string): Promise<void> {
  try {
    await deleteAdminVoice(id)
    message.success('已删除')
    fetchVoices()
  } catch {
    message.error('删除失败')
  }
}

onMounted(() => {
  fetchTasks()
  fetchUsage()
  fetchVoices()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.dubbing-admin {
  padding: @spacing-xl;
}
.dubbing-tabs {
  :deep(.ant-tabs-nav) {
    margin-bottom: 16px;
  }
  :deep(.ant-tabs-content-holder) {
    margin-top: 0;
  }
}
.dubbing-admin :deep(.page-header) {
  margin-bottom: 12px;
}
.player-wrap {
  text-align: center;
}
.player-text {
  font-size: @font-size-base;
  color: @text-primary;
  margin-bottom: @spacing-lg;
  line-height: 1.6;
}
.player-audio {
  width: 100%;
}
</style>
