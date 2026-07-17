<template>
  <a-layout style="height: 100vh; overflow: hidden">
    <a-layout-sider v-model:collapsed="collapsed" :trigger="null" collapsible theme="dark" width="220" class="sider">
      <div class="logo">
        <span v-if="!collapsed" class="logo-text">智浚AI</span>
        <span v-if="!collapsed" class="logo-sub">DredgeAI</span>
        <span v-else class="logo-mini">智</span>
      </div>
      <a-menu v-model:selectedKeys="selectedKeys" theme="dark" mode="inline" class="sider-menu-top" @click="handleMenuClick">
        <template v-if="pinnedApps.length > 0">
          <a-menu-item v-for="app in pinnedApps" :key="app.route">
            <component :is="app.icon" />
            <span>{{ app.title }}</span>
          </a-menu-item>
        </template>
      </a-menu>
      <a-menu v-model:selectedKeys="selectedKeys" theme="dark" mode="inline" class="sider-menu-bottom" @click="handleMenuClick">
        <a-menu-item key="/user/api">
          <ApiOutlined />
          <span>API Keys</span>
        </a-menu-item>
        <a-menu-item key="/user/profile" class="profile-item">
          <a-avatar size="small" :style="{ backgroundColor: '#00c9b7' }">{{ store.user.name[0] }}</a-avatar>
          <span>个人中心</span>
        </a-menu-item>
      </a-menu>
    </a-layout-sider>
    <a-layout class="main-layout">
      <a-layout-header class="user-header">
        <div class="header-left">
          <MenuUnfoldOutlined v-if="collapsed" class="trigger" @click="collapsed = !collapsed" />
          <MenuFoldOutlined v-else class="trigger" @click="collapsed = !collapsed" />
          <a-segmented
            :value="currentRole"
            :options="roleOptions"
            @change="handleRoleChange"
            class="role-segmented"
          />
        </div>
        <div class="header-right">
          <ShopOutlined class="header-icon" @click="showStoreModal = true" />
          <a-badge count="3" :offset="[2, -2]">
            <BellOutlined class="header-icon" />
          </a-badge>
          <a-dropdown>
            <span class="user-info">
              <a-avatar size="small" style="backgroundColor: #00c9b7">{{ store.user.name[0] }}</a-avatar>
              <span class="user-name">{{ store.user.name }}</span>
            </span>
            <template #overlay>
              <a-menu>
                <a-menu-item key="profile"><UserOutlined /> 个人中心</a-menu-item>
                <a-menu-item key="logout"><LogoutOutlined /> 退出登录</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>
      <a-layout-content class="user-content">
        <router-view />
      </a-layout-content>
    </a-layout>

    <a-modal v-model:visible="showStoreModal" title="应用商店" :footer="null" width="640" :destroyOnClose="true">
      <div class="store-list">
        <div v-for="app in authorizedApps" :key="app.id" class="store-item">
          <div class="store-item-info">
            <component :is="getIcon(app.icon || 'AppstoreOutlined')" class="store-icon" />
            <div>
              <div class="store-name">{{ app.title }}</div>
              <div class="store-desc">{{ app.description }}</div>
            </div>
          </div>
          <a-switch
            :checked="pinnedIds.includes(app.id)"
            @change="(checked: boolean) => togglePin(app.id, checked)"
            size="small"
            checked-children="已固定"
            un-checked-children="固定"
          />
        </div>
      </div>
    </a-modal>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'
import type { Component } from 'vue'
import {
  AppstoreOutlined, FileSearchOutlined,
  BookOutlined, UserOutlined, ApiOutlined,
  MenuUnfoldOutlined, MenuFoldOutlined, BellOutlined, LogoutOutlined,
  EditOutlined, SafetyOutlined, DashboardOutlined, QuestionCircleOutlined,
  SwapOutlined, ShopOutlined,
} from '@ant-design/icons-vue'
import { appCards } from '@/mock/data'

const router = useRouter()
const route = useRoute()
const store = useAppStore()

const collapsed = ref(false)
const selectedKeys = ref<string[]>([route.path])
const showStoreModal = ref(false)
const currentRole = ref(route.path.startsWith('/admin') ? 'admin' : 'user')

const roleOptions = [
  { label: '用户端', value: 'user' },
  { label: '管理后台', value: 'admin' },
]

const iconMap: Record<string, Component> = {
  FileSearch: FileSearchOutlined,
  Book: BookOutlined,
  Edit: EditOutlined,
  Safety: SafetyOutlined,
  Dashboard: DashboardOutlined,
  Api: ApiOutlined,
  QuestionCircle: QuestionCircleOutlined,
  Swap: SwapOutlined,
}

function getIcon(name: string): Component {
  return iconMap[name] || AppstoreOutlined
}

const appRouteMap: Record<string, string> = {
  '1': '/user/bid-review',
  '2': '/user/standards',
}

const pinnedIds = ref<string[]>(['1', '2'])

const authorizedApps = computed(() => appCards.filter(a => a.status !== '待申请'))

const pinnedApps = computed(() =>
  appCards
    .filter(a => pinnedIds.value.includes(a.id) && appRouteMap[a.id])
    .map(a => ({
      id: a.id,
      title: a.title,
      route: appRouteMap[a.id],
      icon: getIcon(a.icon || 'AppstoreOutlined'),
    }))
)

function togglePin(id: string, pin: boolean) {
  if (pin) {
    if (!pinnedIds.value.includes(id)) {
      pinnedIds.value = [...pinnedIds.value, id]
    }
  } else {
    pinnedIds.value = pinnedIds.value.filter(i => i !== id)
  }
}

function handleRoleChange(value: string | number) {
  const v = value as 'user' | 'admin'
  currentRole.value = v
  router.push(v === 'admin' ? '/admin/dashboard' : '/user/profile')
}

watch(() => route.path, (p) => {
  selectedKeys.value = [p]
  currentRole.value = p.startsWith('/admin') ? 'admin' : 'user'
})

function handleMenuClick({ key }: { key: string }) {
  router.push(key)
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.sider :deep(.ant-layout-sider-children) {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.main-layout {
  height: 100%;
}
.sider-menu-top {
  flex: 1 1 auto;
  overflow-y: auto;
}
.sider-menu-bottom {
  flex-shrink: 0;
  border-top: 1px solid rgba(255, 255, 255, 0.12);
}

.logo {
  height: 64px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}
.logo-text {
  font-size: 18px;
  font-weight: 700;
  color: @accent-color;
  letter-spacing: 1px;
  line-height: 1.2;
}
.logo-sub {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.45);
  letter-spacing: 0.5px;
  margin-top: 1px;
}
.logo-mini {
  font-size: 20px;
  font-weight: 700;
  color: @accent-color;
}

.user-header {
  background: @card-bg;
  padding: 0 24px;
  display: flex;
  align-items: center;
  height: @header-height;
  box-shadow: @shadow-sm;
  position: sticky;
  top: 0;
  z-index: 100;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}
.role-segmented {
  font-size: 13px;
}
.trigger {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
}
.header-right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 20px;
}
.header-icon {
  font-size: 18px;
  color: @text-secondary;
  cursor: pointer;
}
.user-info {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}
.user-name {
  font-size: 13px;
  color: @text-primary;
}

.user-content {
  overflow-y: auto;
  flex: 1;
}

.profile-item {
  border-top: 1px solid rgba(255, 255, 255, 0.06);
  margin-top: 4px;
}

.store-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.store-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-radius: @border-radius;
  transition: background 0.2s;
  &:hover {
    background: rgba(0, 0, 0, 0.02);
  }
}
.store-item-info {
  display: flex;
  align-items: center;
  gap: 12px;
}
.store-icon {
  font-size: 24px;
  color: @accent-color;
}
.store-name {
  font-size: 14px;
  font-weight: 600;
  color: @text-primary;
}
.store-desc {
  font-size: 12px;
  color: @text-secondary;
  margin-top: 2px;
}
</style>
