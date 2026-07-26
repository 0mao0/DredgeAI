<template>
  <a-layout class="user-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :theme="isDark ? 'dark' : 'light'"
      :width="170"
      :collapsed-width="48"
      class="sider"
    >
      <div class="sider-brand">
        <div class="sider-brand__left" @click="collapsed = !collapsed">
          <div v-if="!collapsed">
            <Logo :collapsed="collapsed" subtitle="DredgeAI" />
          </div>
          <MenuUnfoldOutlined v-else class="sider-brand__expand-icon" />
        </div>
        <MenuFoldOutlined
          v-if="!collapsed"
          class="sider-brand__trigger"
          @click="collapsed = !collapsed"
        />
      </div>
      <a-menu
        v-model:selectedKeys="selectedKeys"
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
        v-model:selectedKeys="selectedKeys"
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
  DashboardOutlined, UserOutlined, ApiOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  BulbFilled, BulbOutlined,
} from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import Logo from '@shared/web/components/Logo.vue'
import { useAppStore } from '@/stores/app'
import { useUserStore } from '@/stores/user'
import { useThemeStore } from '@shared/web/stores'
import type { Component } from 'vue'

const appStore = useAppStore()
const userStore = useUserStore()
const themeStore = useThemeStore()

const isDark = computed(() => {
  if (themeStore.theme === 'auto') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  }
  return themeStore.theme === 'dark'
})

function toggleTheme(): void {
  themeStore.theme = isDark.value ? 'light' : 'dark'
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

const collapsed = ref(false)
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
  justify-content: space-between;

  &__left {
    cursor: pointer;
    flex: 1;
    min-width: 0;
  }

  &__expand-icon {
    display: block;
    margin: 0 auto;
    font-size: 18px;
    color: @header-text-secondary;
    padding: @spacing-md 0;
    text-align: center;
  }

  &__trigger {
    font-size: 14px;
    color: @header-text-secondary;
    cursor: pointer;
    flex-shrink: 0;
    padding-right: @spacing-sm;
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
  overflow-y: auto;
  scrollbar-gutter: stable;
  background: @content-bg;
}
</style>
