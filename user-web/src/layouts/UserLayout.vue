<template>
  <a-layout class="user-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :theme="isDark ? 'dark' : 'light'"
      :width="170"
      :collapsed-width="48"
      breakpoint="lg"
      class="sider"
      @breakpoint="(broken: boolean) => { collapsed = broken }"
    >
      <div class="sider-brand" :class="{ 'sider-brand--collapsed': collapsed }">
        <template v-if="!collapsed">
          <span
            class="sider-brand__trigger"
            role="button"
            title="收起侧栏"
            @click="collapsed = !collapsed"
          >
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
              <rect x="4.5" y="4.5" width="15" height="15" rx="3" />
              <line x1="10.5" y1="4.5" x2="10.5" y2="19.5" />
            </svg>
          </span>
          <div class="sider-brand__name">
            <div class="sider-brand__title">智浚 <span class="sider-brand__ai">AI</span></div>
            <div class="sider-brand__sub">DredgeAI</div>
          </div>
        </template>
        <span
          v-else
          class="sider-brand__expand-icon"
          role="button"
          title="展开侧栏"
          @click="collapsed = !collapsed"
        >
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">
            <path d="M10.5 4.5H7.5a3 3 0 0 0-3 3v9a3 3 0 0 0 3 3h3V4.5Z" fill="currentColor" stroke="none" />
            <rect x="4.5" y="4.5" width="15" height="15" rx="3" />
            <line x1="10.5" y1="4.5" x2="10.5" y2="19.5" />
          </svg>
        </span>
      </div>
      <a-menu
        v-model:selected-keys="selectedKeys"
        :theme="isDark ? 'dark' : 'light'"
        mode="inline"
        class="sider-menu sider-menu--main"
        @click="handleMenuClick"
      >
        <a-menu-item key="/dashboard">
          <DashboardOutlined />
          <span>工作台</span>
        </a-menu-item>
        <a-menu-item v-for="app in appStore.sidebarApps" :key="app.route">
          <component :is="iconMap[app.icon]" />
          <span>{{ app.title }}</span>
        </a-menu-item>
      </a-menu>

      <div class="sider-divider" />

      <a-menu
        v-model:selected-keys="selectedKeys"
        :theme="isDark ? 'dark' : 'light'"
        mode="inline"
        class="sider-menu sider-menu--bottom"
        @click="handleMenuClick"
      >
        <a-menu-item key="/api">
          <ApiOutlined />
          <span>API 管理</span>
        </a-menu-item>
        <a-menu-item key="/profile">
          <UserOutlined />
          <span>个人中心</span>
          <a-tooltip v-if="!collapsed" :title="isDark ? '切换亮色模式' : '切换暗色模式'" placement="top">
            <a-button
              class="profile-theme-btn"
              shape="circle"
              size="small"
              @click.stop="toggleTheme"
            >
              <template #icon>
                <BulbFilled v-if="isDark" />
                <BulbOutlined v-else />
              </template>
            </a-button>
          </a-tooltip>
        </a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout class="main-layout">
      <a-layout-content class="content">
        <router-view v-slot="{ Component, route }">
          <transition name="fade" mode="out-in">
            <component :is="Component" :key="route.path" />
          </transition>
        </router-view>
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined,
  UserOutlined,
  ApiOutlined,
  BulbFilled,
  BulbOutlined,
} from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import { useAppStore } from '@/stores/app'
import { useUserStore } from '@/stores/user'
import { useSidebarStore, useThemeStore } from '@shared/web/stores'
import type { Component } from 'vue'

const appStore = useAppStore()
const userStore = useUserStore()
const themeStore = useThemeStore()
const sidebarStore = useSidebarStore()

const isDark = computed(() => themeStore.isDark)

function toggleTheme(): void {
  themeStore.toggleTheme()
}

const iconMap: Record<string, Component> = {
  FileSearchOutlined: Icons.FileSearchOutlined,
  BookOutlined: Icons.BookOutlined,
  EditOutlined: Icons.EditOutlined,
  SafetyOutlined: Icons.SafetyOutlined,
  DashboardOutlined: Icons.DashboardOutlined,
  ApiOutlined: Icons.ApiOutlined,
  QuestionCircleOutlined: Icons.QuestionCircleOutlined,
  SwapOutlined: Icons.SwapOutlined,
  CodeOutlined: Icons.CodeOutlined,
  TeamOutlined: Icons.TeamOutlined,
  AuditOutlined: Icons.AuditOutlined,
  SearchOutlined: Icons.SearchOutlined,
  WarningOutlined: Icons.WarningOutlined,
  FundOutlined: Icons.FundOutlined,
  VideoCameraOutlined: Icons.VideoCameraOutlined,
  CustomerServiceOutlined: Icons.CustomerServiceOutlined,
  BulbOutlined: Icons.BulbOutlined,
  ToolOutlined: Icons.ToolOutlined,
  FileProtectOutlined: Icons.FileProtectOutlined,
  RadarChartOutlined: Icons.RadarChartOutlined,
  ExperimentOutlined: Icons.ExperimentOutlined,
}

const router = useRouter()
const route = useRoute()

const collapsed = computed({
  get: () => sidebarStore.collapsed,
  set: (v) => { sidebarStore.setCollapsed(v) },
})
const selectedKeys = ref<string[]>([route.path])

watch(() => route.path, (p) => {
  const parent = p.startsWith('/ai-bid/') ? '/ai-bid' : p
  selectedKeys.value = [parent]
})

function handleMenuClick({ key }: { key: string }): void {
  router.push(key)
}

onMounted(() => {
  userStore.fetchUser()
  appStore.fetchApps()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.user-layout { height: 100vh; }

.sider {
  background: @header-bg !important;
  border-right: 1px solid @border-color;
  :deep(.ant-layout-sider-children) { display: flex; flex-direction: column; }
}

.sider-brand {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  height: @header-height;
  // 左内距与菜单项图标对齐（菜单图标视觉左缘 28px）
  padding: 0 @spacing-md 0 calc(@spacing-xl + 4px);

  &--collapsed {
    padding: 0;
  }

  &__trigger {
    color: @header-text-secondary;
    cursor: pointer;
    flex-shrink: 0;
    display: flex;
    align-items: center;
    &:hover { color: @brand-primary; }
  }

  &__name {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  &__title {
    font-size: @font-size-lg;
    font-weight: @font-weight-semibold;
    color: @header-text;
    line-height: 1.2;
    letter-spacing: 0.02em;
  }

  &__ai {
    background: var(--color-brand-gradient);
    -webkit-background-clip: text;
    background-clip: text;
    -webkit-text-fill-color: transparent;
    font-weight: @font-weight-bold;
  }

  &__sub {
    font-size: 11px;
    color: @header-text-secondary;
    letter-spacing: 0.6px;
    line-height: 1.3;
    margin-top: 2px;
  }

  &__expand-icon {
    display: flex;
    align-items: center;
    justify-content: center;
    margin: 0 auto;
    color: @header-text-secondary;
    cursor: pointer;
    &:hover { color: @brand-primary; }
  }
}

.sider-menu {
  border-right: none !important;
  &--main { flex: 1; overflow-y: auto; }
  &--bottom {
    flex-shrink: 0;
    :deep(.ant-menu-item:last-child .ant-menu-title-content) {
      display: flex;
      align-items: center;
    }
  }
}
.sider-divider {
  height: 1px;
  background: @border-color;
  margin: 0 16px;
  flex-shrink: 0;
}

.profile-theme-btn {
  font-size: 14px;
  color: @header-text-secondary;
  margin-left: 8px;
  &:hover { color: @brand-primary; }
}

.main-layout { height: 100%; overflow: hidden; }

.content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  scrollbar-gutter: stable;
  background: @content-bg;
}
</style>
