<template>
  <a-layout class="admin-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :theme="isDark ? 'dark' : 'light'"
      :width="200"
      :collapsed-width="64"
      breakpoint="lg"
      class="sider"
      @breakpoint="(broken: boolean) => { collapsed = broken }"
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
        v-model:selected-keys="selectedKeys"
        v-model:open-keys="openKeys"
        :theme="isDark ? 'dark' : 'light'"
        mode="inline"
        class="sider-menu"
        :items="menuItems"
        @click="handleMenuClick"
      />

      <a-menu
        :selected-keys="[route.path]"
        :theme="isDark ? 'dark' : 'light'"
        mode="inline"
        class="sider-menu-bottom"
        @click="handleMenuClick"
      >
        <a-menu-item key="/api">
          <div class="profile-menu-row">
            <ApiOutlined />
            <span>API 管理</span>
          </div>
        </a-menu-item>
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
import { ref, watch, onMounted, computed, h } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  UserOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  ApiOutlined,
} from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import Logo from '@shared/web/components/Logo.vue'
import { useAppStore } from '@/stores/app'
import { useSidebarStore, useThemeStore } from '@shared/web/stores'
import { getApplications } from '@/api/modules/applications'
import ThemeToggle from '@shared/web/components/ThemeToggle.vue'
import { adminAppManifests, adminMenuGroups } from '@/router/manifests'
import { manifestToMenu, collectMenuKeys } from '@shared/web/router/manifest'
import type { MenuNode } from '@shared/web/router/manifest'
import type { Component, VNode } from 'vue'

const router = useRouter()
const route = useRoute()
const appStore = useAppStore()
const sidebarStore = useSidebarStore()
const themeStore = useThemeStore()

const isDark = computed(() => themeStore.isDark)

const collapsed = computed({
  get: () => sidebarStore.collapsed,
  set: (v) => { sidebarStore.setCollapsed(v) },
})
const selectedKeys = ref<string[]>([route.path])

interface AppMenuItem { id: string, name: string, category: string, catColor: string, route: string, icon?: string }
const appMenuItems = ref<AppMenuItem[]>([])

const catColorMap: Record<string, string> = {
  通用: '#3B82F6',
  经营: '#10B981',
  设计: '#8B5CF6',
  施工: '#F59E0B',
}

// ─── 菜单：由 manifest 生成，动态应用列表并入 apps 分组 ───

interface MenuItemNode {
  key: string
  label?: string | VNode
  icon?: () => VNode
  children?: MenuItemNode[]
}

const menuTree = manifestToMenu(adminAppManifests, adminMenuGroups)
const manifestLeafKeys = collectMenuKeys(menuTree)

function resolveIcon(name?: string): (() => VNode) | undefined {
  if (!name) return undefined
  const comp = (Icons as Record<string, Component>)[name]
  return comp ? () => h(comp) : undefined
}

function toMenuItems(nodes: MenuNode[]): MenuItemNode[] {
  return nodes.map((n) => ({
    key: n.key,
    label: n.title,
    icon: resolveIcon(n.icon),
    ...(n.children && n.children.length > 0 ? { children: toMenuItems(n.children) } : {}),
  }))
}

function appToMenuItem(app: AppMenuItem): MenuItemNode {
  return {
    key: app.route,
    icon: resolveIcon(app.icon),
    label: h('span', { class: 'app-menu-entry' }, [
      h('span', {
        class: 'app-cat-tag',
        style: { color: app.catColor, borderColor: app.catColor, background: `${app.catColor}22` },
      }, app.category),
      h('span', { class: 'app-menu-label' }, app.name),
    ]),
  }
}

const menuItems = computed<MenuItemNode[]>(() => {
  const items = toMenuItems(menuTree)
  const dynamic = appMenuItems.value
    .filter((a) => !manifestLeafKeys.has(a.route))
    .map(appToMenuItem)
  const appsGroup = items.find((i) => i.key === 'apps')
  if (appsGroup) appsGroup.children = [...(appsGroup.children ?? []), ...dynamic]
  return items
})

function getRouteParents(): string[] {
  return (route.meta?.parentKeys as string[]) || []
}

const openKeys = ref<string[]>([])

onMounted(async () => {
  const parents = getRouteParents()
  if (parents) openKeys.value = [...parents]
  try {
    await appStore.fetchProfile()
  } catch {
    message.warning('获取用户信息失败，使用默认配置')
  }
  try {
    const apps = await getApplications()
    appMenuItems.value = apps.map((a) => ({
      id: a.id,
      name: a.name,
      category: a.category,
      catColor: catColorMap[a.category] || '#94A3B8',
      route: a.route || `/applications/${a.id}`,
      icon: a.icon,
    }))
  } catch {
    message.warning('应用列表加载失败，使用默认菜单')
    appMenuItems.value = [
      { id: '1', name: '标准查询', category: '通用', catColor: '#3B82F6', route: '/applications/standard', icon: 'BookOutlined' },
      { id: '2', name: 'AI视频', category: '通用', catColor: '#3B82F6', route: '/applications/ai-video', icon: 'VideoCameraOutlined' },
      { id: '3', name: 'AI 配音', category: '通用', catColor: '#3B82F6', route: '/applications/dubbing', icon: 'CustomerServiceOutlined' },
      { id: '4', name: '设计经验', category: '设计', catColor: '#8B5CF6', route: '/applications/design-experience', icon: 'BulbOutlined' },
      { id: '5', name: '施工经验', category: '施工', catColor: '#F59E0B', route: '/applications/construction-experience', icon: 'ToolOutlined' },
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

<style lang="less">
@import '@shared/web/styles/variables.less';

// 非 scoped：动态应用菜单项由 h() 渲染，不带 scoped data 属性
.app-menu-entry {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
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
</style>
