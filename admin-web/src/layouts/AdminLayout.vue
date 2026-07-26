<template>
  <a-layout class="admin-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :theme="isDark ? 'dark' : 'light'"
      :width="200"
      :collapsed-width="64"
      class="sider"
    >
      <div class="sider-brand">
        <div class="sider-brand__left" @click="collapsed = !collapsed">
          <div v-if="!collapsed">
            <Logo :collapsed="collapsed" subtitle="管理后台" />
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
        v-model:openKeys="openKeys"
        :theme="isDark ? 'dark' : 'light'"
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
          <a-menu-item v-for="app in appMenuItems" :key="app.route">
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
      </a-menu>

      <a-menu
        :selected-keys="[route.path]"
        :theme="isDark ? 'dark' : 'light'"
        mode="inline"
        class="sider-menu-bottom"
        @click="handleMenuClick"
      >
        <a-menu-item key="/profile">
          <div class="profile-menu-row">
            <UserOutlined />
            <span>个人中心</span>
            <span v-if="!collapsed" class="profile-spacer" />
            <ThemeToggle v-if="!collapsed" />
          </div>
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
import { ref, watch, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  CodeOutlined, MenuOutlined, ScheduleOutlined, FileTextOutlined, InfoCircleOutlined,
  TeamOutlined, UsergroupAddOutlined, SafetyOutlined,
  DashboardOutlined, AppstoreOutlined, DatabaseOutlined, BarChartOutlined, ControlOutlined,
  FundOutlined, EyeOutlined, SwapOutlined, BankOutlined, FileSearchOutlined, BulbOutlined,
  ApiOutlined, AlertOutlined, UserOutlined,
  MenuFoldOutlined, MenuUnfoldOutlined,
} from '@ant-design/icons-vue'
import Logo from '@shared/web/components/Logo.vue'
import { useAppStore } from '@/stores/app'
import { useSidebarStore, useThemeStore } from '@shared/web/stores'
import { getProfile } from '@/api/modules/profile'
import { getApplications } from '@/api/modules/applications'
import ThemeToggle from '@shared/web/components/ThemeToggle.vue'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()
const sidebarStore = useSidebarStore()
const themeStore = useThemeStore()

const isDark = computed(() => {
  if (themeStore.theme === 'auto') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  }
  return themeStore.theme === 'dark'
})

const collapsed = computed({
  get: () => sidebarStore.collapsed,
  set: (v) => { sidebarStore.setCollapsed(v) },
})
const selectedKeys = ref<string[]>([route.path])

interface AppMenuItem { id: string; name: string; category: string; catColor: string; route: string }
const appMenuItems = ref<AppMenuItem[]>([])

const catColorMap: Record<string, string> = {
  '通用': '#3B82F6',
  '经营': '#10B981',
  '设计': '#8B5CF6',
  '施工': '#F59E0B',
}

	function getRouteParents(): string[] {
	  return (route.meta?.parentKeys as string[]) || []
	}

	const openKeys = ref<string[]>([])

onMounted(async () => {
  const parents = getRouteParents()
  if (parents) openKeys.value = [...parents]
  if (!appStore.profile) {
    try {
      const user = await getProfile()
      appStore.setProfile(user)
    } catch {
      message.warning('获取用户信息失败，使用默认配置')
    }
  }
  try {
    const apps = await getApplications()
    appMenuItems.value = apps.map((a) => ({
      id: a.id, name: a.name, category: a.category,
      catColor: catColorMap[a.category] || '#94A3B8',
      route: a.route || `/applications/${a.id}`,
    }))
  } catch {
    message.warning('应用列表加载失败，使用默认菜单')
    appMenuItems.value = [
      { id: '1', name: '标准查询', category: '通用', catColor: '#3B82F6', route: '/applications/standard' },
      { id: '2', name: 'AI视频', category: '通用', catColor: '#3B82F6', route: '/applications/ai-video' },
      { id: '3', name: 'AI 配音', category: '通用', catColor: '#3B82F6', route: '/applications/dubbing' },
      { id: '4', name: '设计经验', category: '设计', catColor: '#8B5CF6', route: '/applications/design-experience' },
      { id: '5', name: '施工经验', category: '施工', catColor: '#F59E0B', route: '/applications/construction-experience' },
    ]
  }
})

watch(() => route.path, (p) => {
  selectedKeys.value = [p]
  const parents = getRouteParents()
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
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.admin-layout { height: 100vh; }

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
  flex: 1;
  border-right: none !important;
  overflow-y: auto;
}

.sider-menu-bottom {
  flex-shrink: 0;
  border-right: none !important;
}

.profile-menu-row {
  display: flex;
  align-items: center;
  width: 100%;
}

.profile-spacer {
  flex: 1;
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

.content {
  flex: 1;
  overflow-y: auto;
  background: @content-bg;
  transition: background @transition-base;
}
</style>
