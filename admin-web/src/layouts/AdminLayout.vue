<template>
  <a-layout class="admin-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      theme="dark"
      :width="240"
      :collapsed-width="64"
      class="sider"
    >
      <div class="logo">
        <div class="logo-mark">智</div>
        <div v-if="!collapsed" class="logo-text">
          <div class="logo-name">智浚 AI</div>
          <div class="logo-sub">管理后台</div>
        </div>
      </div>

      <a-menu
        v-model:selectedKeys="selectedKeys"
        v-model:openKeys="openKeys"
        theme="dark"
        mode="inline"
        class="sider-menu"
        @click="handleMenuClick"
      >
        <a-menu-item-group key="main" title="">
          <template #title>
            <span v-if="!collapsed" class="menu-group-label">导航</span>
          </template>
          <a-menu-item key="/dashboard">
            <DashboardOutlined />
            <span>仪表盘</span>
          </a-menu-item>
          <a-menu-item key="/permissions">
            <SafetyOutlined />
            <span>权限管理</span>
          </a-menu-item>
          <a-menu-item key="/applications">
            <AppstoreOutlined />
            <span>应用管理</span>
          </a-menu-item>
          <a-menu-item key="/data">
            <DatabaseOutlined />
            <span>数据源</span>
          </a-menu-item>
          <a-menu-item key="/analytics">
            <BarChartOutlined />
            <span>数据分析</span>
          </a-menu-item>
        </a-menu-item-group>

        <a-menu-divider class="menu-divider" />

        <a-menu-item-group key="account" title="">
          <template #title>
            <span v-if="!collapsed" class="menu-group-label">账号</span>
          </template>
          <a-menu-item key="/profile">
            <UserOutlined />
            <span>个人中心</span>
          </a-menu-item>
          <a-menu-item key="/api">
            <ApiOutlined />
            <span>API 管理</span>
          </a-menu-item>
        </a-menu-item-group>
      </a-menu>
    </a-layout-sider>

    <a-layout class="main-layout">
      <a-layout-header class="header">
        <div class="header-left">
          <component
            :is="collapsed ? MenuUnfoldOutlined : MenuFoldOutlined"
            class="trigger"
            @click="collapsed = !collapsed"
          />
          <a-breadcrumb class="breadcrumb">
            <a-breadcrumb-item>{{ route.meta.title as string }}</a-breadcrumb-item>
          </a-breadcrumb>
        </div>
        <div class="header-right">
          <a-badge dot>
            <BellOutlined class="header-icon" />
          </a-badge>
          <ThemeToggle />
          <a-dropdown>
            <span class="user-info">
              <a-avatar :style="{ background: '#0EA5E9' }">
                {{ profile?.name?.[0] || 'A' }}
              </a-avatar>
              <span class="user-name">{{ profile?.name || '管理员' }}</span>
            </span>
            <template #overlay>
              <a-menu @click="handleUserMenu">
                <a-menu-item key="profile"><UserOutlined /> 个人中心</a-menu-item>
                <a-menu-item key="logout"><LogoutOutlined /> 退出登录</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>

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
import { ref, watch, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, SafetyOutlined, AppstoreOutlined, DatabaseOutlined,
  BarChartOutlined, UserOutlined, ApiOutlined,
  MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'
import { useAppStore } from '@/stores/app'
import { getProfile } from '@/api/modules/profile'
import ThemeToggle from '@shared/components/ThemeToggle.vue'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()

const collapsed = computed({
  get: () => appStore.sidebarCollapsed,
  set: (v) => { appStore.sidebarCollapsed = v },
})
const selectedKeys = ref<string[]>([route.path])
const openKeys = ref<string[]>([])

const profile = computed(() => appStore.profile)

onMounted(async () => {
  if (!appStore.profile) {
    try {
      const user = await getProfile()
      appStore.setProfile(user)
    } catch {
      // mock fallback
    }
  }
})

watch(() => route.path, (p) => { selectedKeys.value = [p] })

function handleMenuClick({ key }: { key: string }): void {
  router.push(key)
}

function handleUserMenu({ key }: { key: string }): void {
  if (key === 'profile') router.push('/profile')
  else if (key === 'logout') {
    router.push('/dashboard')
  }
}
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';

.admin-layout { height: 100vh; }

.sider {
  background: @sidebar-bg !important;
  :deep(.ant-layout-sider-children) { display: flex; flex-direction: column; }
}

.logo {
  height: @header-height;
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: 0 @spacing-xl;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.logo-mark {
  width: 32px; height: 32px;
  border-radius: @radius-base;
  background: @brand-gradient;
  display: flex; align-items: center; justify-content: center;
  color: white;
  font-weight: @font-weight-bold;
  font-size: @font-size-lg;
  flex-shrink: 0;
}
.logo-name {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: white;
  line-height: 1.2;
}
.logo-sub {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.4);
  letter-spacing: 1px;
}

.menu-group-label {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: rgba(255, 255, 255, 0.3);
  padding-left: 4px;
}

.menu-divider {
  margin: 8px 16px;
  background: rgba(255, 255, 255, 0.06);
}

.sider-menu {
  flex: 1;
  border-right: none !important;
  overflow-y: auto;
}

.main-layout { height: 100%; overflow: hidden; }

.header {
  background: var(--antd-header-bg);
  padding: 0 @spacing-xl;
  display: flex;
  align-items: center;
  height: @header-height;
  box-shadow: @shadow-sm;
  z-index: 10;
  transition: background @transition-base;
}
.header-left {
  display: flex;
  align-items: center;
  gap: @spacing-lg;
  flex: 1;
}
.trigger {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
  &:hover { color: @brand-primary; }
}
.breadcrumb {
  :deep(.ant-breadcrumb-separator) { color: @text-tertiary; }
  :deep(.ant-breadcrumb-link) { color: @text-secondary; font-size: @font-size-sm; }
}
.header-right {
  display: flex;
  align-items: center;
  gap: @spacing-xl;
}
.header-icon {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
  &:hover { color: @brand-primary; }
}
.user-info {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  cursor: pointer;
}
.user-name {
  font-size: @font-size-sm;
  color: @text-primary;
}

.content {
  flex: 1;
  overflow-y: auto;
  background: @content-bg;
  transition: background @transition-base;
}
</style>
