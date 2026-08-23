<template>
  <a-layout class="admin-layout">
    <a-layout-sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :theme="isDark ? 'dark' : 'light'"
      :width="176"
      :collapsed-width="64"
      breakpoint="lg"
      class="sider"
      @breakpoint="(broken: boolean) => { collapsed = broken }"
    >
      <div class="sider-brand" :class="{ 'sider-brand--collapsed': collapsed }">
        <div v-if="!collapsed" class="sider-brand__name">
          <div class="sider-brand__title-row">
            <ShipAiLogo class="sider-brand__logo" />
            <div class="sider-brand__text">
              <div class="sider-brand__title">智浚 <span class="sider-brand__ai">AI</span></div>
              <div class="sider-brand__sub">管理后台</div>
            </div>
          </div>
        </div>
        <span
          v-if="!collapsed"
          class="sider-brand__trigger"
          role="button"
          title="收起侧栏"
          @click="collapsed = !collapsed"
        >
          <SidebarToggleIcon :collapsed="false" />
        </span>
        <span
          v-else
          class="sider-brand__expand-icon"
          role="button"
          title="展开侧栏"
          @click="collapsed = !collapsed"
        >
          <SidebarToggleIcon :collapsed="true" />
        </span>
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
            <ThemeToggle v-if="!collapsed" @click.stop />
          </div>
        </a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout class="main-layout">
      <a-layout-content class="content">
        <router-view v-slot="{ Component: viewComponent, route: viewRoute }">
          <transition name="fade" mode="out-in">
            <component :is="viewComponent" :key="viewRoute.path" />
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
  ApiOutlined,
} from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import { ShipAiLogo, SidebarToggleIcon } from '@shared/web'
import { useAppStore } from '@/stores/app'
import { useSidebarStore, useThemeStore } from '@shared/web/stores'
import { getAppOrder, getApplications } from '@/api/modules/applications'
import { sortAppsByOrder } from '@/utils/appOrder'
import { getCategoryColor, getCategoryAlphaBg } from '@shared/core/utils'
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

interface AppMenuItem { id: string, name: string, category: string, route: string, icon?: string }
const appMenuItems = ref<AppMenuItem[]>([])

// ─── 菜单：由 manifest 生成，动态应用列表并入 apps 分组 ───

interface MenuItemNode {
  key: string
  label?: string | VNode
  icon?: () => VNode
  children?: MenuItemNode[]
  type?: 'divider'
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
        style: {
          color: getCategoryColor(app.category),
          borderColor: getCategoryColor(app.category),
          background: getCategoryAlphaBg(app.category),
        },
      }, app.category),
      h('span', { class: 'app-menu-label' }, app.name),
    ]),
  }
}

const menuItems = computed<MenuItemNode[]>(() => {
  const items = toMenuItems(menuTree)

  // 在基础配置组后插入分隔线
  const configIdx = items.findIndex((i) => i.key === 'base-config')
  if (configIdx !== -1) {
    items.splice(configIdx + 1, 0, { key: 'divider-1', type: 'divider' } as MenuItemNode)
  }

  // 在知识库组后插入分隔线
  const knowledgeIdx = items.findIndex((i) => i.key === 'knowledge')
  if (knowledgeIdx !== -1) {
    items.splice(knowledgeIdx + 1, 0, { key: 'divider-2', type: 'divider' } as MenuItemNode)
  }

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
    const [apps, orderRes] = await Promise.all([
      getApplications(),
      getAppOrder().catch(() => null),
    ])
    // 应用菜单与发布管理的顺序保持一致（按 admin 默认顺序排序）
    const orderedApps = sortAppsByOrder(apps, orderRes?.appIds ?? [])
    appMenuItems.value = orderedApps.map((a) => ({
      id: a.id,
      name: a.name,
      category: a.category,
      route: a.route || `/applications/${a.id}`,
      icon: a.icon,
    }))
  } catch {
    message.warning('应用列表加载失败，使用默认菜单')
    appMenuItems.value = [
      { id: '1', name: '规范问答', category: '通用', route: '/applications/standard', icon: 'BookOutlined' },
      { id: '2', name: 'AI视频', category: '通用', route: '/applications/ai-video', icon: 'VideoCameraOutlined' },
      { id: '3', name: 'AI 配音', category: '通用', route: '/applications/dubbing', icon: 'CustomerServiceOutlined' },
      { id: '4', name: '设计经验', category: '设计', route: '/applications/design-experience', icon: 'BulbOutlined' },
      { id: '5', name: '施工经验', category: '施工', route: '/applications/construction-experience', icon: 'ToolOutlined' },
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
  justify-content: flex-start;
  gap: @spacing-md;
  height: @header-height;
  box-sizing: border-box;
  padding: 0 @spacing-md 0 calc(@spacing-xl + 4px);

  &--collapsed {
    padding: 0;
  }

  &__name {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  &__title-row {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
  }

  &__logo {
    flex-shrink: 0;
  }

  &__text {
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

  &__trigger {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    color: @header-text-secondary;
    cursor: pointer;
    flex-shrink: 0;
    border-radius: @radius-base;
    transition: color @transition-fast, background @transition-fast;
    &:hover {
      color: @brand-primary;
      background: color-mix(in srgb, @brand-primary 8%, transparent);
    }
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
