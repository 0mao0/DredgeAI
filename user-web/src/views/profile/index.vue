<template>
  <div class="profile-page">
    <!-- 个人信息横幅（紧凑） -->
    <div class="profile-banner">
      <div class="banner-bg" />
      <div class="banner-main">
        <div class="banner-avatar-wrap">
          <a-avatar :size="64" class="banner-avatar">
            <template #icon><UserOutlined /></template>
          </a-avatar>
          <div class="banner-status-dot" />
        </div>
        <div class="banner-info">
          <div class="banner-name">
            {{ userStore.userInfo?.name || '用户' }}
            <a-tag color="blue" class="banner-role-tag">{{ userStore.userInfo?.position }}</a-tag>
          </div>
          <div class="banner-meta">
            <span class="banner-meta-item"><TeamOutlined />{{ userStore.userInfo?.department }}</span>
            <span class="banner-meta-item"><MailOutlined />{{ userStore.userInfo?.email }}</span>
            <span class="banner-meta-item"><PhoneOutlined />{{ userStore.userInfo?.phone }}</span>
          </div>
        </div>
        <div class="banner-stats-inline">
          <div class="stat-block">
            <div class="stat-block-top">
              <AppstoreOutlined style="color: var(--color-brand)" />
              <span>{{ appStore.authorizedApps.length }}</span>
            </div>
            <div class="stat-block-label">已授权应用</div>
          </div>
          <div class="stat-block">
            <div class="stat-block-top">
              <CheckCircleOutlined style="color: var(--color-success)" />
              <span>{{ appStore.visibleAppRoutes.length }}</span>
            </div>
            <div class="stat-block-label">已启用应用</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 下方内容：左窄（偏好设置） + 右宽（应用设置） -->
    <div class="profile-content">
      <!-- 左侧：偏好设置（紧凑） -->
      <div class="profile-left">
        <div class="section-card">
          <div class="sc-header">偏好设置</div>
          <div class="sc-body">
            <div class="pref-group">
              <div class="pref-label">界面主题</div>
              <div class="theme-row">
                <div
                  v-for="opt in themeOptions"
                  :key="opt.value"
                  class="theme-chip"
                  :class="{ active: themeStore.theme === opt.value }"
                  @click="themeStore.theme = opt.value"
                >
                  <div class="theme-dot" :class="opt.value" />
                  <span>{{ opt.label }}</span>
                </div>
              </div>
            </div>
            <a-divider class="pref-divider" />
            <div class="pref-group">
              <div class="pref-label">语言</div>
              <div class="theme-row">
                <div
                  v-for="opt in langOptions"
                  :key="opt.value"
                  class="theme-chip"
                  :class="{ active: preferences.language === opt.value }"
                  @click="preferences.language = opt.value"
                >
                  <span>{{ opt.label }}</span>
                </div>
              </div>
            </div>
            <a-divider class="pref-divider" />
            <div class="pref-group">
              <div class="pref-label">通知偏好</div>
              <div class="notif-checks">
                <label
                  v-for="opt in notifOptions"
                  :key="opt.value"
                  class="notif-check"
                >
                  <a-checkbox
                    :checked="preferences.notifications.includes(opt.value)"
                    @change="toggleNotif(opt.value)"
                  />
                  <span>{{ opt.label }}</span>
                </label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 右侧：应用设置（主体） -->
      <div class="profile-right">
        <div class="section-card">
          <div class="sc-header">
            <div class="sc-header-left">
              <span>应用设置</span>
              <span class="app-count">（{{ sidebarApps.length }} / {{ appStore.authorizedApps.length }}）</span>
            </div>
            <div class="category-bar">
              <span v-for="cat in categoryOptions" :key="cat.key" class="cat-tag" :style="{ color: catColorMap[cat.key], borderColor: catColorMap[cat.key], background: `${catColorMap[cat.key]}22` }">{{ cat.label }}<span class="cat-tag-count">({{ cat.count }})</span></span>
            </div>
          </div>
          <div class="sc-body">
            <div ref="appListRef" class="app-grid">
              <div
                v-for="(app, idx) in filteredApps"
                :key="app.route || app.id"
                class="app-card"
                :class="{
                  'active': !!app.route && appStore.visibleAppRoutes.includes(app.route),
                  'dragging': dragIndex === idx,
                  'over-top': dragOverIndex === idx && dragOverDir === 'top',
                  'over-bottom': dragOverIndex === idx && dragOverDir === 'bottom',
                  'disabled': !app.route,
                }"
                :draggable="!!app.route && appStore.visibleAppRoutes.includes(app.route)"
                @dragstart="onDragStart(idx, $event)"
                @dragover="onDragOver(idx, $event)"
                @dragend="onDragEnd"
                @drop="onDrop(idx)"
              >
                <div class="app-card-drag"><HolderOutlined /></div>
                <div
                  class="app-card-icon"
                  :style="{ '--app-icon-color': catColorMap[app.category] || '#94A3B8' }"
                >
                  <component :is="iconMap[app.icon]" />
                </div>
                <div class="app-card-body">
                  <div class="app-card-name-row">
                    <span class="app-cat-pill" :style="catPillStyle(app.category)">{{ app.category }}</span>
                    <span class="app-card-name" :style="{ color: catColorMap[app.category] || 'var(--color-text-primary)' }">{{ app.title }}</span>
                  </div>
                  <span class="app-card-desc">{{ app.description }}</span>
                </div>
                <a-switch
                  v-if="app.route"
                  :checked="appStore.visibleAppRoutes.includes(app.route)"
                  size="small"
                  @change="appStore.toggleAppRoute(app.route)"
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  HolderOutlined,
  UserOutlined,
  TeamOutlined,
  MailOutlined,
  PhoneOutlined,
  AppstoreOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import { useUserStore } from '@/stores/user'
import { useAppStore } from '@/stores/app'
import { useThemeStore } from '@shared/web/stores'
import type { Component } from 'vue'

const userStore = useUserStore()
const appStore = useAppStore()
const themeStore = useThemeStore()

// 应用图标映射（覆盖所有已定义的应用图标）
const iconMap: Record<string, Component> = {
  BookOutlined: Icons.BookOutlined,
  VideoCameraOutlined: Icons.VideoCameraOutlined,
  CustomerServiceOutlined: Icons.CustomerServiceOutlined,
  BulbOutlined: Icons.BulbOutlined,
  ToolOutlined: Icons.ToolOutlined,
  FileProtectOutlined: Icons.FileProtectOutlined,
  DashboardOutlined: Icons.DashboardOutlined,
  RadarChartOutlined: Icons.RadarChartOutlined,
  ExperimentOutlined: Icons.ExperimentOutlined,
  FileSearchOutlined: Icons.FileSearchOutlined,
  AppstoreOutlined: Icons.AppstoreOutlined,
}

// 主题选项
const themeOptions = [
  { value: 'light' as const, label: '浅色' },
  { value: 'dark' as const, label: '深色' },
  { value: 'auto' as const, label: '自动' },
]

// 语言选项
const langOptions = [
  { value: 'zh-CN', label: '简体中文' },
  { value: 'en-US', label: 'English' },
]

// 偏好设置
const preferences = ref({
  language: 'zh-CN',
  notifications: ['business', 'system'],
})

// 通知选项
const notifOptions = [
  { label: '业务通知', value: 'business' },
  { label: '系统通知', value: 'system' },
  { label: '审计日志', value: 'audit' },
]

// 分类定义（顺序固定：通用 | 设计 | 施工 | 经营），计数动态计算
const CATEGORY_DEFS: Array<'通用' | '设计' | '施工' | '经营'> = ['通用', '设计', '施工', '经营']

// 分类选项（含各分类下的应用数量）
const categoryOptions = computed(() => {
  const apps = appStore.authorizedApps.filter((a) => a.route)
  return CATEGORY_DEFS.map((key) => ({
    key,
    label: key,
    count: apps.filter((a) => a.category === key).length,
  }))
})

// 切换通知偏好
function toggleNotif(value: string): void {
  const idx = preferences.value.notifications.indexOf(value)
  if (idx === -1) {
    preferences.value.notifications.push(value)
  } else {
    preferences.value.notifications.splice(idx, 1)
  }
}

const sidebarApps = computed(() => appStore.sidebarApps)

const catColorMap: Record<string, string> = {
  通用: '#3B82F6',
  经营: '#10B981',
  设计: '#8B5CF6',
  施工: '#F59E0B',
}

function catPillStyle(category: string) {
  const c = catColorMap[category] || '#94A3B8'
  return { color: c, borderColor: c, background: `${c}22` }
}

// 按可见顺序排序，已激活在前、未激活在后
const filteredApps = computed(() => {
  return appStore.authorizedApps
    .filter((a) => a.route)
    .sort((a, b) => {
      const ai = appStore.visibleAppRoutes.indexOf(a.route!)
      const bi = appStore.visibleAppRoutes.indexOf(b.route!)
      if (ai === -1 && bi === -1) return 0
      if (ai === -1) return 1
      if (bi === -1) return -1
      return ai - bi
    })
})

// 拖拽状态
const dragIndex = ref(-1)
const dragOverIndex = ref(-1)
const dragOverDir = ref<'top' | 'bottom'>('bottom')
const appListRef = ref<HTMLElement>()

function onDragStart(idx: number, e: DragEvent): void {
  dragIndex.value = idx
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', String(idx))
  }
}

function onDragOver(idx: number, e: DragEvent): void {
  e.preventDefault()
  if (e.dataTransfer) e.dataTransfer.dropEffect = 'move'
  if (idx === dragIndex.value) return
  const el = appListRef.value?.children[idx] as HTMLElement | undefined
  if (!el) return
  const rect = el.getBoundingClientRect()
  const mid = rect.top + rect.height / 2
  dragOverIndex.value = idx
  dragOverDir.value = e.clientY < mid ? 'top' : 'bottom'
}

function onDragEnd(): void {
  dragIndex.value = -1
  dragOverIndex.value = -1
}

function onDrop(idx: number): void {
  if (dragIndex.value === -1 || dragIndex.value === idx) { onDragEnd(); return }
  const routes = [...appStore.visibleAppRoutes]
  const fromIdx = routes.indexOf(filteredApps.value[dragIndex.value].route!)
  const toIdx = routes.indexOf(filteredApps.value[idx].route!)
  if (fromIdx === -1 || toIdx === -1) { onDragEnd(); return }
  const [moved] = routes.splice(fromIdx, 1)
  routes.splice(toIdx, 0, moved)
  appStore.setVisibleRoutes(routes)
  onDragEnd()
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

// 页面容器 - 全宽，无最大宽度限制
.profile-page {
  padding: @spacing-xl;
  padding-bottom: @spacing-3xl;
}

// ========== 个人信息横幅 ==========
.profile-banner {
  position: relative;
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  overflow: hidden;
  margin-bottom: @spacing-xl;
}

.banner-bg {
  position: absolute;
  inset: 0;
  height: 100px;
  background: var(--color-brand-gradient);
  opacity: 0.06;
  mask-image: linear-gradient(to bottom, rgba(0,0,0,1) 0%, rgba(0,0,0,0) 100%);
  -webkit-mask-image: linear-gradient(to bottom, rgba(0,0,0,1) 0%, rgba(0,0,0,0) 100%);
}

.banner-main {
  position: relative;
  display: flex;
  align-items: center;
  gap: @spacing-lg;
  padding: @spacing-xl @spacing-xl @spacing-lg;
}

.banner-avatar-wrap {
  position: relative;
  flex-shrink: 0;
}

.banner-avatar {
  border: 2px solid @card-bg;
  box-shadow: 0 0 0 2px var(--color-brand), @shadow-brand;
  background: var(--color-brand-gradient);
  font-size: 26px;
}

.banner-status-dot {
  position: absolute;
  bottom: 2px;
  right: 2px;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: @success;
  border: 2px solid @card-bg;
}

.banner-info { flex: 1; min-width: 0; }

.banner-name {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  font-size: @font-size-xl;
  font-weight: @font-weight-bold;
  color: @text-primary;
}

.banner-role-tag {
  font-size: @font-size-xs;
  border-radius: 20px;
  padding: 0 8px;
  line-height: 20px;
}

.banner-meta {
  display: flex;
  flex-wrap: wrap;
  gap: @spacing-lg;
  margin-top: @spacing-xs;
}

.banner-meta-item {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: @font-size-sm;
  color: @text-secondary;
  .anticon { font-size: 13px; color: @text-tertiary; }
}

// 方块统计
.banner-stats-inline {
  display: flex;
  gap: @spacing-sm;
  margin-left: auto;
  flex-shrink: 0;
}

.stat-block {
  width: 80px;
  padding: @spacing-sm @spacing-xs;
  background: @content-bg;
  border-radius: @radius-base;
  border: 1px solid @border-color;
  text-align: center;
}

.stat-block-top {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  font-size: @font-size-lg;
  font-weight: @font-weight-bold;
  color: @text-primary;
  line-height: 1.3;
  .anticon { font-size: 15px; }
}

.stat-block-label {
  font-size: 11px;
  color: @text-tertiary;
  margin-top: 1px;
  line-height: 1.3;
}

// ========== 内容区域 ==========
.profile-content {
  display: flex;
  gap: @spacing-xl;
  align-items: flex-start;
}

// 左侧偏好设置 - 窄栏
.profile-left {
  flex: 0 0 280px;
  min-width: 0;
}

// 右侧应用设置 - 主体
.profile-right {
  flex: 1;
  min-width: 0;
}

// ========== 通用卡片样式 ==========
.section-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
}

.sc-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: @spacing-md @spacing-xl;
  border-bottom: 1px solid @divider-color;
  font-size: @font-size-base;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}

.sc-header-left {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.sc-header .category-bar {
  margin-bottom: 0;
}

.app-count {
  font-size: @font-size-sm;
  color: @text-tertiary;
  font-weight: @font-weight-regular;
  font-variant-numeric: tabular-nums;
}

.sc-body {
  padding: @spacing-base @spacing-xl @spacing-xl;
}

// ========== 偏好设置 ==========
.pref-group {
  & + & { margin-top: 0; }
}

.pref-label {
  font-size: @font-size-sm;
  color: @text-tertiary;
  margin-bottom: @spacing-sm;
}

.pref-divider {
  margin: @spacing-md 0;
}

// 主题 / 语言 选择器 - 紧凑 chip 风格
.theme-row {
  display: flex;
  gap: 6px;
}

.theme-chip {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: @spacing-sm 6px;
  border-radius: @radius-sm;
  border: 1px solid @border-color;
  font-size: @font-size-xs;
  color: @text-secondary;
  cursor: pointer;
  transition: all @transition-fast;
  user-select: none;

  &:hover {
    border-color: var(--color-brand);
    color: var(--color-brand);
  }

  &.active {
    border-color: var(--color-brand);
    background: color-mix(in srgb, var(--color-brand) 8%, @card-bg);
    color: var(--color-brand);
    font-weight: @font-weight-medium;
  }
}

.theme-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;

  &.light { background: #F59E0B; }
  &.dark { background: #1E293B; }
  &.auto { background: linear-gradient(135deg, #F59E0B 50%, #1E293B 50%); }
}

// 通知偏好 checkbox
.notif-checks {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.notif-check {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-xs 0;
  font-size: @font-size-sm;
  color: @text-primary;
  cursor: pointer;
}

// ========== 应用设置 ==========
.category-bar {
  display: inline-flex;
  gap: @spacing-sm;
  align-items: center;
}

.cat-tag {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 1px 8px;
  height: 22px;
  font-size: @font-size-xs;
  font-weight: 600;
  border: 1px solid;
  border-radius: 4px;
  white-space: nowrap;
}

.cat-tag-count {
  font-size: @font-size-xs;
  color: @text-tertiary;
  font-variant-numeric: tabular-nums;
}

.app-cat-pill {
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

.app-card-name-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  min-width: 0;
}

// 应用列表
.app-grid {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.app-card {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border: 1px solid transparent;
  border-radius: @radius-base;
  background: @card-bg;
  transition: all @transition-fast;

  &:not(.disabled):hover {
    border-color: @border-color;
    background: @content-bg;
  }

  &.active {
    border-color: var(--color-brand);
    background: color-mix(in srgb, var(--color-brand) 4%, @card-bg);
  }

  &.disabled {
    opacity: 0.35;
    cursor: not-allowed;
  }

  &.dragging {
    opacity: 0.3;
    border-style: dashed;
    border-color: var(--color-brand);
    transform: scale(0.98);
  }

  &.over-top { border-top: 2px solid var(--color-brand); }
  &.over-bottom { border-bottom: 2px solid var(--color-brand); }
}

.app-card-drag {
  color: @text-tertiary;
  cursor: grab;
  font-size: 14px;
  flex-shrink: 0;

  .disabled & { cursor: not-allowed; }
}

.app-card-icon {
  width: 34px;
  height: 34px;
  border-radius: @radius-base;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  color: var(--app-icon-color);
  background: color-mix(in srgb, var(--app-icon-color) 10%, transparent);
  flex-shrink: 0;
  transition: transform @transition-fast;

  .app-card:not(.disabled):hover & { transform: scale(1.1); }
}

.app-card-body {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: baseline;
  gap: @spacing-sm;
}

.app-card-name {
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
  white-space: nowrap;
}

.app-card-desc {
  font-size: @font-size-xs;
  color: @text-tertiary;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
