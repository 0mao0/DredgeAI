<template>
  <div class="page-container">
    <div class="welcome-banner">
      <div class="welcome-left">
        <h1 class="welcome-title">
          你好，{{ userStore.userInfo?.name || '用户' }} 👋
        </h1>
        <p class="welcome-desc">
          {{ userStore.userInfo?.position }} · {{ userStore.userInfo?.department }} · 今天有 {{ pendingTaskCount }} 个任务待处理
        </p>
        <div class="welcome-tags">
          <a-tag v-for="scope in (userStore.userInfo?.authorizedScopes || []).slice(0, 4)" :key="scope" color="cyan">
            {{ scope }}
          </a-tag>
        </div>
      </div>
      <div class="welcome-right">
        <div class="quick-task-grid">
          <div
            v-for="(task, i) in appStore.quickTasks"
            :key="task.id"
            class="quick-task-card"
            :style="itemStyle(i)"
            @click="router.push(task.route)"
          >
            <component :is="iconMap[task.icon]" class="quick-task-icon" />
            <div class="quick-task-info">
              <div class="quick-task-title">{{ task.title }}</div>
              <div class="quick-task-tag">{{ task.tag }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <a-row :gutter="[24, 24]" class="main-row">
      <a-col :xs="24" :lg="16">
        <SectionCard title="最近任务" class="mb-24" flush>
          <a-list :data-source="appStore.tasks" :loading="loading">
            <template #renderItem="{ item }">
              <a-list-item class="task-item">
                <a-list-item-meta>
                  <template #title>
                    <span class="task-title" @click="router.push('/ai-bid')">{{ item.title }}</span>
                  </template>
                  <template #description>
                    <span class="task-meta">{{ item.app }} · 更新于 {{ item.updatedAt }}</span>
                  </template>
                  <template #avatar>
                    <div class="task-avatar" :class="`task-avatar--${item.status}`">
                      <component :is="statusIconMap[item.status]" />
                    </div>
                  </template>
                </a-list-item-meta>
                <div class="task-right">
                  <StatusTag :status="item.status" />
                  <a-progress v-if="item.progress !== undefined" :percent="item.progress" size="small" class="task-progress" />
                </div>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>

        <SectionCard title="本周效率趋势">
          <ChartContainer :option="efficiencyChartOption" height="280px" :loading="chartLoading" />
        </SectionCard>
      </a-col>

      <a-col :xs="24" :lg="8">
        <SectionCard title="最近文件" flush>
          <a-list :data-source="appStore.files" :loading="loading" size="small">
            <template #renderItem="{ item }">
              <a-list-item class="file-item">
                <a-list-item-meta>
                  <template #title>
                    <span class="file-name">{{ item.name }}</span>
                  </template>
                  <template #description>
                    <span class="file-meta">{{ item.size }} · {{ item.updatedAt }}</span>
                  </template>
                  <template #avatar>
                    <div class="file-icon" :class="`file-icon--${item.type}`">
                      <component :is="fileIconMap[item.type]" />
                    </div>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  FileSearchOutlined,
  BookOutlined,
  EditOutlined,
  FilePdfOutlined,
  FileWordOutlined,
  FileExcelOutlined,
  FileImageOutlined,
  FileOutlined,
  CheckCircleOutlined,
  SyncOutlined,
  PauseCircleOutlined,
  CloseCircleOutlined,
} from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import StatusTag from '@/components/StatusTag.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import { useUserStore } from '@/stores/user'
import { useAppStore } from '@/stores/app'
import { getEfficiencyTrend } from '@/api/modules/chart'
import type { LineChartData } from '@/types'
import type { Component } from 'vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { useStaggerReveal } from '@shared/web/composables/useStaggerReveal'

const router = useRouter()
const userStore = useUserStore()
const appStore = useAppStore()

const { itemStyle } = useStaggerReveal(() => appStore.quickTasks.length, 60, 'background 0.2s ease')

const loading = ref(false)
const chartLoading = ref(false)

const iconMap: Record<string, Component> = {
  FileSearchOutlined,
  BookOutlined,
  EditOutlined,
}
const statusIconMap: Record<string, Component> = {
  已完成: CheckCircleOutlined,
  进行中: SyncOutlined,
  已暂停: PauseCircleOutlined,
  已失败: CloseCircleOutlined,
}
const fileIconMap: Record<string, Component> = {
  pdf: FilePdfOutlined,
  docx: FileWordOutlined,
  xlsx: FileExcelOutlined,
  image: FileImageOutlined,
  other: FileOutlined,
}

const pendingTaskCount = computed(() => appStore.tasks.filter((t) => t.status === '进行中' || t.status === '已暂停').length)

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')

const efficiencyTrend = ref<LineChartData>({ categories: [], series: [] })

const efficiencyChartOption = computed(() => ({
  tooltip: { trigger: 'axis' },
  legend: { data: ['任务数', '完成数'], bottom: 0 },
  grid: { left: '3%', right: '4%', bottom: '10%', top: '5%', containLabel: true },
  xAxis: { type: 'category', boundaryGap: false, data: efficiencyTrend.value.categories },
  yAxis: { type: 'value' },
  series: efficiencyTrend.value.series.map((s, i) => ({
    name: s.name,
    type: 'line',
    smooth: true,
    data: s.data,
    itemStyle: { color: i === 0 ? brandColor.value : successColor.value },
    areaStyle: { opacity: 0.1 },
  })),
}))

onMounted(async () => {
  loading.value = true
  chartLoading.value = true
  await Promise.all([
    appStore.fetchTasks(),
    appStore.fetchQuickTasks(),
    appStore.fetchFiles(),
    appStore.fetchApps(),
    getEfficiencyTrend().then((d) => { efficiencyTrend.value = d }),
  ])
  loading.value = false
  chartLoading.value = false
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.mb-24 { margin-bottom: @spacing-xl; }

.welcome-banner {
  background: @brand-gradient;
  border-radius: @radius-xl;
  padding: @spacing-2xl;
  margin-bottom: @spacing-xl;
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: white;
  box-shadow: @shadow-brand;

  @media (max-width: 992px) {
    flex-direction: column;
    align-items: flex-start;
    gap: @spacing-base;
  }
}
.welcome-title {
  font-size: @font-size-3xl;
  font-weight: @font-weight-bold;
  margin-bottom: @spacing-sm;
}
.welcome-desc {
  font-size: @font-size-base;
  opacity: 0.9;
  margin-bottom: @spacing-md;
}
.welcome-tags { display: flex; gap: @spacing-xs; flex-wrap: wrap; }

.quick-task-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: @spacing-md;
  min-width: 360px;

  @media (max-width: 992px) {
    min-width: 0;
    width: 100%;
  }
}
.quick-task-card {
  background: rgba(255, 255, 255, 0.15);
  backdrop-filter: blur(8px);
  border-radius: @radius-base;
  padding: @spacing-md @spacing-base;
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  cursor: pointer;
  transition: background @transition-base;
  &:hover { background: rgba(255, 255, 255, 0.25); }
}
.quick-task-icon { font-size: 24px; color: white; }
.quick-task-title { font-size: @font-size-sm; font-weight: @font-weight-medium; color: white; }
.quick-task-tag { font-size: 10px; opacity: 0.8; color: white; }

.task-item {
  padding: @spacing-md 0 !important;
  &:hover .task-title { color: @brand-primary; }
}
.task-title { cursor: pointer; font-weight: @font-weight-medium; transition: color @transition-base; }
.task-meta { font-size: @font-size-xs; color: @text-tertiary; }
.task-avatar {
  width: 40px; height: 40px;
  border-radius: @radius-base;
  display: flex; align-items: center; justify-content: center;
  font-size: 18px;
  &--已完成 { background: color-mix(in srgb, @success 12%, transparent); color: @success; }
  &--进行中 { background: color-mix(in srgb, @brand-primary 12%, transparent); color: @brand-primary; }
  &--已暂停 { background: color-mix(in srgb, @warning 12%, transparent); color: @warning; }
  &--已失败 { background: color-mix(in srgb, @danger 12%, transparent); color: @danger; }
}
.task-right { display: flex; flex-direction: column; align-items: flex-end; gap: @spacing-xs; }
.task-progress { width: 120px; }

.app-list { display: flex; flex-direction: column; gap: @spacing-sm; }
.app-item {
  display: flex; align-items: center; gap: @spacing-md;
  padding: @spacing-sm;
  border-radius: @radius-base;
  cursor: pointer;
  transition: background @transition-base;
  &:hover { background: @content-bg; }
}
.app-icon-wrap {
  width: 36px; height: 36px;
  border-radius: @radius-base;
  background: color-mix(in srgb, @brand-primary 10%, transparent);
  color: @brand-primary;
  display: flex; align-items: center; justify-content: center;
  font-size: 16px;
  flex-shrink: 0;
}
.app-name { font-size: @font-size-sm; font-weight: @font-weight-medium; color: @text-primary; }
.app-desc { font-size: @font-size-xs; color: @text-tertiary; .truncate-1(); }

.file-item { padding: @spacing-sm 0 !important; }
.file-name { font-size: @font-size-sm; color: @text-primary; }
.file-meta { font-size: @font-size-xs; color: @text-tertiary; }
.file-icon {
  width: 32px; height: 32px;
  border-radius: @radius-sm;
  display: flex; align-items: center; justify-content: center;
  font-size: 16px;
  &--pdf { background: color-mix(in srgb, @danger 12%, transparent); color: @danger; }
  &--docx { background: color-mix(in srgb, @info 12%, transparent); color: @info; }
  &--xlsx { background: color-mix(in srgb, @success 12%, transparent); color: @success; }
  &--image { background: color-mix(in srgb, @warning 12%, transparent); color: @warning; }
  &--other { background: @divider-color; color: @text-secondary; }
}

.truncate-1() {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
