<template>
  <div class="page-container ai-bid">
    <div class="bid-header">
      <div class="bid-header-left">
        <h2 class="bid-title">AI投标</h2>
        <div class="bid-nav">
          <div
            v-for="feature in features"
            :key="feature.route"
            class="nav-card"
            :class="{ 'nav-card--active': $route.path.startsWith(feature.route) }"
            @click="goFeature(feature.route)"
          >
            <div class="nav-icon" :style="{ background: feature.bg }">
              <component :is="feature.icon" />
            </div>
            <span>{{ feature.name }}</span>
          </div>
        </div>
      </div>
      <div class="bid-header-right">
        <span class="bid-history-trigger" @click="sessionDrawer = true">历史记录</span>
      </div>
    </div>

    <div class="ai-bid-content">
      <router-view v-slot="{ Component }">
        <transition name="fade-slide" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </div>

    <a-drawer
      v-model:open="sessionDrawer"
      title="历史记录"
      placement="right"
      width="400"
      destroy-on-close
    >
      <div class="session-filter">
        <a-segmented
          v-model:value="historyTypeFilter"
          :options="historyFilterOptions"
          size="small"
        />
      </div>
      <a-skeleton v-if="historyLoading" active :paragraph="{ rows: 5 }" />
      <a-empty v-else-if="!filteredHistoryItems.length" :description="historyEmptyText" />
      <div v-else class="session-list">
        <div
          v-for="item in filteredHistoryItems"
          :key="item.id"
          class="session-item"
          @click="openHistory(item)"
        >
          <div class="session-head">
            <span class="session-type" :class="`session-type--${item.kind}`">{{ item.type }}</span>
            <span class="session-name" :title="item.name">{{ item.name }}</span>
            <span class="session-spacer" />
            <span class="session-status" :class="item.statusTone">{{ item.statusText }}</span>
          </div>
          <div class="session-meta">
            <span>{{ item.time }}</span>
            <span v-if="item.detail" class="session-badge" :class="item.detailTone">{{ item.detail }}</span>
          </div>
        </div>
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import {
  FileSearchOutlined,
  EditOutlined,
  SwapOutlined,
  ClearOutlined,
} from '@ant-design/icons-vue'
import { getBidSessions } from '@/api/modules/bid'
import { getTasks } from '@/api/modules/compare'
import { useSidebarStore } from '@shared/web/stores'
import { COMPARE_STATUS_MAP } from './compare/constants'
import type { BidReviewSession, CompareTask } from '@/types'

const router = useRouter()
const sidebarStore = useSidebarStore()
const sessionDrawer = ref(false)
const historyItems = ref<HistoryItem[]>([])
const historyLoading = ref(false)
const historyTypeFilter = ref<'all' | 'compare' | 'read'>('all')
const historyFilterOptions = [
  { label: '全部', value: 'all' },
  { label: '比标', value: 'compare' },
  { label: '读标', value: 'read' },
]
const filteredHistoryItems = computed(() =>
  historyTypeFilter.value === 'all'
    ? historyItems.value
    : historyItems.value.filter((i) => i.kind === historyTypeFilter.value),
)
const historyEmptyText = computed(() => {
  const label = historyFilterOptions.find((o) => o.value === historyTypeFilter.value)?.label
  return historyTypeFilter.value === 'all' ? '暂无历史记录' : `暂无${label}记录`
})

const compareStatusMap = COMPARE_STATUS_MAP

interface HistoryItem {
  id: string
  kind: 'compare' | 'read'
  type: string
  name: string
  statusText: string
  statusTone: string
  time: string
  sortTime: number
  detail?: string
  detailTone?: string
  raw: CompareTask | BidReviewSession
}

function formatDateTime(value: string): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const pad = (n: number): string => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function compareStatusTone(status: CompareTask['status']): string {
  if (status === 'completed') return 'session-status--ok'
  if (status === 'partial') return 'session-status--warn'
  if (status === 'failed') return 'session-status--bad'
  return 'session-status--run'
}

watch(sessionDrawer, async (open) => {
  if (!open) return
  historyLoading.value = true
  try {
    const [compareTasks, sessions] = await Promise.all([getTasks(), getBidSessions()])
    const items: HistoryItem[] = [
      ...compareTasks.map((t) => ({
        id: `compare-${t.id}`,
        kind: 'compare' as const,
        type: '比标',
        name: t.name,
        statusText: compareStatusMap[t.status]?.text ?? t.status,
        statusTone: compareStatusTone(t.status),
        time: formatDateTime(t.createdAt),
        sortTime: Date.parse(t.createdAt) || 0,
        detail: `${t.documents.length} 份标书`,
        detailTone: 'session-badge--ok',
        raw: t,
      })),
      ...sessions.map((s) => ({
        id: `read-${s.id}`,
        kind: 'read' as const,
        type: '读标',
        name: s.document,
        statusText: s.status,
        statusTone: s.status === '已完成' ? 'session-status--ok' : 'session-status--run',
        time: formatDateTime(s.date),
        sortTime: Date.parse(s.date.replace(' ', 'T')) || 0,
        detail: `${s.riskCount} 项风险`,
        detailTone: s.riskCount > 0 ? 'session-badge--warn' : 'session-badge--ok',
        raw: s,
      })),
    ].sort((a, b) => b.sortTime - a.sortTime)
    historyItems.value = items
  } catch {
    historyItems.value = []
  } finally {
    historyLoading.value = false
  }
})

function openHistory(item: HistoryItem): void {
  sessionDrawer.value = false
  if (item.kind === 'compare') {
    const task = item.raw as CompareTask
    router.push({ path: '/ai-bid/compare', query: { task: task.id } })
  } else {
    router.push('/ai-bid/read')
  }
}

const features = [
  { route: '/ai-bid/read', name: '读标', icon: FileSearchOutlined, bg: 'linear-gradient(135deg, color-mix(in srgb, var(--color-info) 80%, black), var(--color-info))' },
  { route: '/ai-bid/write', name: '写标', icon: EditOutlined, bg: 'linear-gradient(135deg, color-mix(in srgb, var(--color-success) 80%, black), var(--color-success))' },
  { route: '/ai-bid/compare', name: '比标', icon: SwapOutlined, bg: 'linear-gradient(135deg, color-mix(in srgb, var(--color-warning) 80%, black), var(--color-warning))' },
  { route: '/ai-bid/clear', name: '清标', icon: ClearOutlined, bg: 'linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 80%, black), var(--color-accent))' },
]

function goFeature(routePath: string): void {
  sidebarStore.setCollapsed(true)
  router.push(routePath)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.bid-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid @border-color;
  padding-bottom: @spacing-md;
  margin-bottom: @spacing-lg;
}

.bid-header-left {
  display: flex;
  align-items: center;
  gap: @spacing-lg;
}

.bid-title {
  font-size: @font-size-2xl;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  margin: 0;
  white-space: nowrap;
}

.bid-nav {
  display: flex;
  align-items: center;
  gap: 6px;
}

.nav-card {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 12px 5px 8px;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  cursor: pointer;
  transition: all @transition-fast;
  box-shadow: @shadow-sm;
  &:hover {
    border-color: @brand-primary;
    box-shadow: @shadow-brand;
    transform: translateY(-1px);
  }
  &--active {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 4%, transparent);
    box-shadow: @shadow-brand;
  }
}

.nav-icon {
  width: 22px;
  height: 22px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  color: white;
  flex-shrink: 0;
}

.bid-header-right {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.bid-history-trigger {
  font-size: @font-size-sm;
  color: @text-secondary;
  cursor: pointer;
  user-select: none;
  transition: color @transition-fast;

  &:hover {
    color: @brand-primary;
  }
}

/* —— 历史记录抽屉 —— */
.session-filter {
  margin-bottom: @spacing-sm;
}
.session-list { display: flex; flex-direction: column; }
.session-item {
  padding: @spacing-md @spacing-lg;
  border-bottom: 1px solid @divider-color;
  cursor: pointer;
  transition: all @transition-base;
  border-radius: @radius-base;
  &:hover { background: @content-bg; }
}
.session-head {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: 4px;
}
.session-type {
  flex-shrink: 0;
  padding: 1px 8px;
  border-radius: @radius-sm;
  font-size: 11px;
  line-height: 18px;
  &--compare { background: color-mix(in srgb, @warning 12%, transparent); color: @warning; }
  &--read { background: color-mix(in srgb, @info 12%, transparent); color: @info; }
}
.session-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
}
.session-spacer { flex: 1; }
.session-status {
  flex-shrink: 0;
  font-size: @font-size-xs;
  &--ok { color: @success; }
  &--warn { color: @warning; }
  &--bad { color: @danger; }
  &--run { color: @brand-primary; }
}
.session-meta {
  display: flex; align-items: center; justify-content: space-between;
  font-size: @font-size-xs; color: @text-tertiary;
}
.session-badge {
  font-size: 11px;
  padding: 1px 8px;
  border-radius: 10px;
  &--ok { background: color-mix(in srgb, @success 12%, transparent); color: @success; }
  &--warn { background: color-mix(in srgb, @danger 12%, transparent); color: @danger; }
}

.ai-bid {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.ai-bid-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
}
</style>

<style lang="less">
.fade-slide-enter-active,
.fade-slide-leave-active {
  transition: all 0.25s ease;
}
.fade-slide-enter-from {
  opacity: 0;
  transform: translateY(10px);
}
.fade-slide-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}
</style>
