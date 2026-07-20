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
      <Logo :collapsed="collapsed" subtitle="管理后台" />

      <a-menu
        v-model:selectedKeys="selectedKeys"
        v-model:openKeys="openKeys"
        theme="dark"
        mode="inline"
        class="sider-menu"
        @click="handleMenuClick"
      >
        <a-sub-menu key="dev">
          <template #title>
            <CodeOutlined />
            <span>开发管理</span>
          </template>
          <a-menu-item key="/menu-config">
            <MenuOutlined />
            <span>菜单配置</span>
          </a-menu-item>
          <a-menu-item key="/task-scheduler">
            <ScheduleOutlined />
            <span>任务调度</span>
          </a-menu-item>
          <a-menu-item key="/logs">
            <FileTextOutlined />
            <span>日志管理</span>
          </a-menu-item>
          <a-menu-item key="/platform">
            <InfoCircleOutlined />
            <span>平台信息</span>
          </a-menu-item>
        </a-sub-menu>

        <a-sub-menu key="users">
          <template #title>
            <TeamOutlined />
            <span>用户权限</span>
          </template>
          <a-menu-item key="/org-users">
            <UsergroupAddOutlined />
            <span>组织用户</span>
          </a-menu-item>
          <a-menu-item key="/permissions">
            <SafetyOutlined />
            <span>权限管理</span>
          </a-menu-item>
        </a-sub-menu>

        <a-menu-item key="/dashboard">
          <DashboardOutlined />
          <span>仪表盘</span>
        </a-menu-item>
        <a-menu-item key="/api">
          <ApiOutlined />
          <span>API 管理</span>
        </a-menu-item>
        <a-menu-item key="/dubbing">
          <CustomerServiceOutlined />
          <span>AI 配音</span>
        </a-menu-item>
        <a-sub-menu key="apps">
          <template #title>
            <AppstoreOutlined />
            <span>应用管理</span>
          </template>
          <a-menu-item key="/applications/analysis">
            <BarChartOutlined />
            <span>数据分析</span>
          </a-menu-item>
          <a-menu-item key="/applications/control">
            <ControlOutlined />
            <span>发布管理</span>
          </a-menu-item>
          <a-menu-item v-for="app in appMenuItems" :key="`/applications/${app.id}`">
            <span class="app-cat-tag" :style="{ color: app.catColor, borderColor: app.catColor, background: app.catColor + '22' }">{{ app.category }}</span>
            <span class="app-menu-label">{{ app.name }}</span>
          </a-menu-item>
        </a-sub-menu>
        <a-sub-menu key="data">
          <template #title>
            <DatabaseOutlined />
            <span>数据仓库</span>
          </template>
          <a-menu-item key="/data/statistics">
            <BarChartOutlined />
            <span>数据统计</span>
          </a-menu-item>
          <a-sub-menu key="data-dynamic">
            <template #title>
              <FundOutlined />
              <span>动态数据</span>
            </template>
            <a-menu-item key="/data/dynamic/monitoring">
              <EyeOutlined />
              <span>监控</span>
            </a-menu-item>
            <a-menu-item key="/data/dynamic/tide-level">
              <SwapOutlined />
              <span>潮位</span>
            </a-menu-item>
          </a-sub-menu>
          <a-sub-menu key="data-static">
            <template #title>
              <DatabaseOutlined />
              <span>静态数据</span>
            </template>
            <a-menu-item key="/data/static/enterprise">
              <BankOutlined />
              <span>企业库</span>
            </a-menu-item>
            <a-menu-item key="/data/static/standards">
              <FileTextOutlined />
              <span>标准库</span>
            </a-menu-item>
            <a-menu-item key="/data/static/reports">
              <FileSearchOutlined />
              <span>报告库</span>
            </a-menu-item>
            <a-menu-item key="/data/static/experience">
              <BulbOutlined />
              <span>经验库</span>
            </a-menu-item>
          </a-sub-menu>
        </a-sub-menu>
        <a-menu-item key="/alerts">
          <AlertOutlined />
          <span>预警管理</span>
        </a-menu-item>

        <a-menu-divider class="menu-divider menu-divider--push" />

        <a-menu-item key="/profile">
          <UserOutlined />
          <span>个人中心</span>
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
              <a-avatar :style="{ background: 'var(--color-brand)' }">
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
  CodeOutlined, MenuOutlined, ScheduleOutlined, FileTextOutlined, InfoCircleOutlined,
  TeamOutlined, UsergroupAddOutlined, SafetyOutlined,
  DashboardOutlined, AppstoreOutlined, DatabaseOutlined, BarChartOutlined, ControlOutlined,
  FundOutlined, EyeOutlined, SwapOutlined, BankOutlined, FileSearchOutlined, BulbOutlined,
  ApiOutlined, AlertOutlined, UserOutlined, CustomerServiceOutlined,
  MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'
import Logo from '@shared/web/components/Logo.vue'
import { useAppStore } from '@/stores/app'
import { useSidebarStore } from '@shared/web/stores'
import { getProfile } from '@/api/modules/profile'
import { getApplications } from '@/api/modules/applications'
import ThemeToggle from '@shared/web/components/ThemeToggle.vue'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()
const sidebarStore = useSidebarStore()

const collapsed = computed({
  get: () => sidebarStore.collapsed,
  set: (v) => { sidebarStore.setCollapsed(v) },
})
const selectedKeys = ref<string[]>([route.path])

interface AppMenuItem { id: string; name: string; category: string; catColor: string }
const appMenuItems = ref<AppMenuItem[]>([])

const catColorMap: Record<string, string> = {
  '通用': '#3B82F6',
  '经营': '#10B981',
  '设计': '#8B5CF6',
  '施工': '#F59E0B',
}

const routeParentsMap: Record<string, string[]> = {
  '/menu-config': ['dev'],
  '/task-scheduler': ['dev'],
  '/logs': ['dev'],
  '/platform': ['dev'],
  '/org-users': ['users'],
  '/permissions': ['users'],
  '/applications/analysis': ['apps'],
  '/applications/control': ['apps'],
  '/data/statistics': ['data'],
  '/data/dynamic/monitoring': ['data', 'data-dynamic'],
  '/data/dynamic/tide-level': ['data', 'data-dynamic'],
  '/data/static/enterprise': ['data', 'data-static'],
  '/data/static/standards': ['data', 'data-static'],
  '/data/static/reports': ['data', 'data-static'],
  '/data/static/experience': ['data', 'data-static'],
  '/dubbing': [],
}

function getRouteParents(p: string): string[] | undefined {
  if (routeParentsMap[p]) return routeParentsMap[p]
  if (p.startsWith('/applications/') && p !== '/applications/analysis' && p !== '/applications/control') return ['apps']
  return undefined
}

const openKeys = ref<string[]>([])

const profile = computed(() => appStore.profile)

onMounted(async () => {
  const parents = getRouteParents(route.path)
  if (parents) openKeys.value = [...parents]
  if (!appStore.profile) {
    try {
      const user = await getProfile()
      appStore.setProfile(user)
    } catch {
      // mock fallback
    }
  }
  try {
    const apps = await getApplications()
    appMenuItems.value = apps.map((a) => ({
      id: a.id, name: a.name, category: a.category,
      catColor: catColorMap[a.category] || '#94A3B8',
    }))
  } catch {
    appMenuItems.value = [
      { id: '1', name: '智能问答', category: '通用', catColor: '#3B82F6' },
      { id: '2', name: '经营看板', category: '经营', catColor: '#10B981' },
      { id: '3', name: 'BIM 建模', category: '设计', catColor: '#8B5CF6' },
      { id: '4', name: '进度追踪', category: '施工', catColor: '#F59E0B' },
      { id: '5', name: '文档分析', category: '通用', catColor: '#3B82F6' },
    ]
  }
})

watch(() => route.path, (p) => {
  selectedKeys.value = [p]
  const parents = getRouteParents(p)
  if (parents) {
    for (const parent of parents) {
      if (!openKeys.value.includes(parent)) {
        openKeys.value = [...openKeys.value, parent]
      }
    }
  }
})

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
@import '@shared/web/styles/variables.less';

.admin-layout { height: 100vh; }

.sider {
  background: @sidebar-bg !important;
  :deep(.ant-layout-sider-children) { display: flex; flex-direction: column; }
}

.menu-divider {
  margin: 8px 16px;
  background: rgba(255, 255, 255, 0.06);
}

.menu-divider--push {
  margin-top: auto;
  margin-bottom: 4px;
}

.sider-menu {
  flex: 1;
  border-right: none !important;
  overflow-y: auto;
}

.app-cat-tag {
  display: inline-flex;
  align-items: center;
  flex-shrink: 0;
  padding: 0 5px;
  height: 18px;
  line-height: 16px;
  font-size: 11px;
  font-weight: 600;
  border: 1px solid;
  border-radius: 3px;
  white-space: nowrap;
}

.app-menu-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.main-layout { height: 100%; overflow: hidden; }

.header {
  background: @sidebar-bg;
  padding: 0 @spacing-xl;
  display: flex;
  align-items: center;
  height: @header-height;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
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
  color: rgba(255, 255, 255, 0.65);
  cursor: pointer;
  &:hover { color: #fff; }
}
.breadcrumb {
  :deep(.ant-breadcrumb-separator) { color: rgba(255, 255, 255, 0.35); }
  :deep(.ant-breadcrumb-link) { color: rgba(255, 255, 255, 0.65); font-size: @font-size-sm; }
}
.header-right {
  display: flex;
  align-items: center;
  gap: @spacing-xl;
}
.header-icon {
  font-size: 18px;
  color: rgba(255, 255, 255, 0.65);
  cursor: pointer;
  &:hover { color: #fff; }
}
.user-info {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  cursor: pointer;
}
.user-name {
  font-size: @font-size-sm;
  color: rgba(255, 255, 255, 0.85);
}

.content {
  flex: 1;
  overflow-y: auto;
  background: @content-bg;
  transition: background @transition-base;
}
</style>
