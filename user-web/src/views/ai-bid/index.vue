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
        <AppButton size="sm" @click="sessionDrawer = true">
          <HistoryOutlined /> 历史记录
        </AppButton>
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
      :title="isCompare ? '比标历史' : '历史会话'"
      placement="right"
      width="400"
      destroy-on-close
    >
      <!-- 比标任务历史 -->
      <div v-if="isCompare" class="session-list">
        <a-skeleton v-if="compareLoading" active :paragraph="{ rows: 4 }" />
        <a-empty v-else-if="!compareTasks.length" description="暂无比标任务" />
        <template v-else>
          <div
            v-for="task in compareTasks"
            :key="task.id"
            class="session-item"
            @click="openCompareTask(task.id)"
          >
            <div class="session-name">
              {{ task.name }}
              <a-tag :color="compareStatusMap[task.status]?.color" class="session-status">
                {{ compareStatusMap[task.status]?.text }}
              </a-tag>
            </div>
            <div class="session-meta">
              <span>{{ task.documents.length }} 份标书 · {{ formatDate(task.createdAt) }}</span>
              <span v-if="task.riskSummary" class="session-badge session-badge--warn">
                高 {{ task.riskSummary.high }} / 中 {{ task.riskSummary.mid }} / 低 {{ task.riskSummary.low }}
              </span>
              <span v-else class="session-badge session-badge--ok">—</span>
            </div>
          </div>
        </template>
      </div>

      <!-- 读标会话历史 -->
      <div v-else class="session-list">
        <div
          v-for="session in sessions"
          :key="session.id"
          class="session-item"
          :class="{ active: session.id === activeSessionId }"
          @click="selectSession(session.id)"
        >
          <div class="session-name">{{ session.document }}</div>
          <div class="session-meta">
            <span>{{ session.date }}</span>
            <span
              class="session-badge"
              :class="session.riskCount > 0 ? 'session-badge--warn' : 'session-badge--ok'"
            >
              {{ session.riskCount }} 风险
            </span>
          </div>
        </div>
      </div>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  FileSearchOutlined,
  EditOutlined,
  SwapOutlined,
  ClearOutlined,
  HistoryOutlined,
} from '@ant-design/icons-vue'
import { getBidSessions } from '@/api/modules/bid'
import { getTasks } from '@/api/modules/compare'
import { useSidebarStore } from '@shared/web/stores'
import { COMPARE_STATUS_MAP } from './compare/constants'
import type { BidReviewSession, CompareTask } from '@/types'

const route = useRoute()
const router = useRouter()
const sidebarStore = useSidebarStore()
const sessionDrawer = ref(false)
const sessions = ref<BidReviewSession[]>([])
const activeSessionId = ref('')

const isCompare = computed(() => route.path.startsWith('/ai-bid/compare'))
const compareTasks = ref<CompareTask[]>([])
const compareLoading = ref(false)

const compareStatusMap = COMPARE_STATUS_MAP

function formatDate(s: string): string {
  return s ? s.slice(0, 10) : '—'
}

watch(sessionDrawer, async (open) => {
  if (!open || !isCompare.value) return
  compareLoading.value = true
  try {
    compareTasks.value = await getTasks()
  } catch {
    compareTasks.value = []
  } finally {
    compareLoading.value = false
  }
})

function openCompareTask(id: string): void {
  sessionDrawer.value = false
  router.push({ path: '/ai-bid/compare', query: { task: id } })
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

function selectSession(id: string): void {
  activeSessionId.value = id
  router.push('/ai-bid/read')
  sessionDrawer.value = false
}

getBidSessions().then((s) => { sessions.value = s; if (s.length > 0) activeSessionId.value = s[0].id })
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

/* —— 历史记录抽屉 —— */
.session-list { display: flex; flex-direction: column; }
.session-item {
  padding: @spacing-md @spacing-lg;
  border-bottom: 1px solid @divider-color;
  cursor: pointer;
  transition: all @transition-base;
  border-radius: @radius-base;
  &:hover { background: @content-bg; }
  &.active { background: color-mix(in srgb, @brand-primary 6%, transparent); }
}
.session-name { font-size: @font-size-sm; font-weight: @font-weight-medium; color: @text-primary; margin-bottom: 4px; }
.session-status { margin-left: @spacing-sm; }
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
