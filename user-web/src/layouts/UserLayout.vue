<template>
  <a-layout class="user-layout">
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
          <div class="logo-sub">DredgeAI</div>
        </div>
      </div>
      <a-menu
        v-model:selectedKeys="selectedKeys"
        theme="dark"
        mode="inline"
        class="sider-menu"
        @click="handleMenuClick"
      >
        <a-menu-item key="/dashboard">
          <DashboardOutlined />
          <span>工作台</span>
        </a-menu-item>
        <a-menu-item key="/apps">
          <AppstoreOutlined />
          <span>应用广场</span>
        </a-menu-item>
        <a-menu-item key="/bid-review">
          <FileSearchOutlined />
          <span>AI 审标</span>
        </a-menu-item>
        <a-menu-item key="/standards">
          <BookOutlined />
          <span>标准查询</span>
        </a-menu-item>
        <a-menu-item key="/profile">
          <UserOutlined />
          <span>个人中心</span>
        </a-menu-item>
        <a-menu-item key="/api">
          <ApiOutlined />
          <span>API 管理</span>
        </a-menu-item>
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
          <a-input-search
            placeholder="搜索应用、任务、标准..."
            class="header-search"
            @search="handleGlobalSearch"
          />
        </div>
        <div class="header-right">
          <a-badge :count="userStore.unreadCount" :offset="[2, -2]">
            <BellOutlined class="header-icon" @click="showNotifications = true" />
          </a-badge>
          <a-tooltip title="管理后台">
            <SettingOutlined class="header-icon" @click="goAdmin" />
          </a-tooltip>
          <a-dropdown>
            <span class="user-info">
              <a-avatar :style="{ background: '@{brand-gradient}' }">
                {{ userStore.userInfo?.name?.[0] || 'U' }}
              </a-avatar>
              <span class="user-name">{{ userStore.userInfo?.name || '用户' }}</span>
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
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </a-layout-content>
    </a-layout>

    <a-drawer
      v-model:open="showNotifications"
      title="通知中心"
      placement="right"
      width="380"
    >
      <a-list :data-source="userStore.notifications" item-layout="vertical">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <a-tag :color="notifColorMap[item.type]" size="small">{{ notifLabelMap[item.type] }}</a-tag>
                <span class="notif-title">{{ item.title }}</span>
              </template>
              <template #description>{{ item.content }}</template>
            </a-list-item-meta>
            <div class="notif-time">{{ item.time }}</div>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, AppstoreOutlined, FileSearchOutlined, BookOutlined,
  UserOutlined, ApiOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, SettingOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'
import { useUserStore } from '@/stores/user'
import { ADMIN_WEB_URL } from '@/utils/constants'
import type { Notification } from '@/types'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const collapsed = ref(false)
const selectedKeys = ref<string[]>([route.path])
const showNotifications = ref(false)

const notifColorMap: Record<Notification['type'], string> = {
  system: 'blue',
  business: 'green',
  audit: 'orange',
}
const notifLabelMap: Record<Notification['type'], string> = {
  system: '系统',
  business: '业务',
  audit: '审计',
}

onMounted(() => {
  if (!userStore.userInfo) userStore.fetchUser()
  userStore.fetchNotifications()
})

watch(() => route.path, (p) => { selectedKeys.value = [p] })

function handleMenuClick({ key }: { key: string }): void {
  router.push(key)
}

function handleGlobalSearch(value: string): void {
  if (!value) return
  router.push({ path: '/apps', query: { q: value } })
}

function goAdmin(): void {
  window.location.href = ADMIN_WEB_URL
}

function handleUserMenu({ key }: { key: string }): void {
  if (key === 'profile') router.push('/profile')
  else if (key === 'logout') {
    localStorage.removeItem('DREDGE_AI_TOKEN')
    router.push('/dashboard')
  }
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.user-layout { height: 100vh; }

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
  width: 32px;
  height: 32px;
  border-radius: @radius-base;
  background: @brand-gradient;
  display: flex;
  align-items: center;
  justify-content: center;
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

.sider-menu {
  flex: 1;
  border-right: none !important;
}

.main-layout { height: 100%; overflow: hidden; }

.header {
  background: @card-bg;
  padding: 0 @spacing-xl;
  display: flex;
  align-items: center;
  height: @header-height;
  box-shadow: @shadow-sm;
  z-index: 10;
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
.header-search {
  width: 320px;
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
}

.notif-title { margin-left: 8px; font-weight: 500; }
.notif-time { font-size: 12px; color: @text-tertiary; margin-top: 4px; }
</style>
