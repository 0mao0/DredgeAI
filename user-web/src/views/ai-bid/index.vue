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
            @click="$router.push(feature.route)"
          >
            <div class="nav-icon" :style="{ background: feature.bg }">
              <component :is="feature.icon" />
            </div>
            <span>{{ feature.name }}</span>
          </div>
        </div>
      </div>
      <div class="bid-header-right">
        <a-button size="small" @click="sessionDrawer = true">
          <HistoryOutlined /> 历史记录
        </a-button>
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
      title="历史会话"
      placement="right"
      width="400"
      destroy-on-close
    >
      <div class="session-list">
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
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  FileSearchOutlined,
  EditOutlined,
  SwapOutlined,
  ClearOutlined,
  HistoryOutlined,
} from '@ant-design/icons-vue'
import { getBidSessions } from '@/api/modules/bid'
import type { BidReviewSession } from '@/types'

const router = useRouter()
const sessionDrawer = ref(false)
const sessions = ref<BidReviewSession[]>([])
const activeSessionId = ref('')

const features = [
  { route: '/ai-bid/read', name: '读标', icon: FileSearchOutlined, bg: 'linear-gradient(135deg, #2563EB, #3B82F6)' },
  { route: '/ai-bid/write', name: '写标', icon: EditOutlined, bg: 'linear-gradient(135deg, #059669, #10B981)' },
  { route: '/ai-bid/compare', name: '比标', icon: SwapOutlined, bg: 'linear-gradient(135deg, #D97706, #F59E0B)' },
  { route: '/ai-bid/clear', name: '清标', icon: ClearOutlined, bg: 'linear-gradient(135deg, #7C3AED, #8B5CF6)' },
]

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

.ai-bid-content {
  min-height: 400px;
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
