<template>
  <div class="page-container">
    <PageHeader title="个人中心" description="账号信息与个性化设置" />

    <SectionCard class="profile-card">
      <div class="profile-top-row">
        <a-avatar :size="64" :style="{ background: 'var(--color-brand-gradient)' }">
          {{ userStore.userInfo?.name?.[0] || 'U' }}
        </a-avatar>
        <div class="profile-info">
          <div class="profile-name">{{ userStore.userInfo?.name || '用户' }}</div>
          <div class="profile-meta">
            <span>{{ userStore.userInfo?.position }}</span>
            <em>·</em>
            <span>{{ userStore.userInfo?.department }}</span>
          </div>
        </div>
      </div>
      <a-descriptions :column="2" size="small" class="profile-desc">
        <a-descriptions-item label="邮箱">{{ userStore.userInfo?.email }}</a-descriptions-item>
        <a-descriptions-item label="电话">{{ userStore.userInfo?.phone }}</a-descriptions-item>
      </a-descriptions>
    </SectionCard>

    <a-row :gutter="24" class="profile-bottom">
      <a-col :xs="24" :lg="7">
        <SectionCard title="偏好设置">
          <a-form layout="vertical">
            <a-form-item label="界面主题">
              <a-radio-group v-model:value="themeStore.theme">
                <a-radio-button value="light">浅色</a-radio-button>
                <a-radio-button value="dark">深色</a-radio-button>
                <a-radio-button value="auto">跟随系统</a-radio-button>
              </a-radio-group>
            </a-form-item>
            <a-form-item label="语言">
              <a-radio-group v-model:value="preferences.language">
                <a-radio-button value="zh-CN">简体中文</a-radio-button>
                <a-radio-button value="en-US">English</a-radio-button>
              </a-radio-group>
            </a-form-item>
            <a-form-item label="通知偏好">
              <a-checkbox-group v-model:value="preferences.notifications" :options="notifOptions" />
            </a-form-item>
          </a-form>
        </SectionCard>
      </a-col>

      <a-col :xs="24" :lg="17">
        <SectionCard title="应用设置">
          <template #extra>
            <a-tag>{{ sidebarApps.length }} / {{ filteredApps.length }} 已启用</a-tag>
          </template>
          <a-typography-text type="secondary" class="app-desc">
            开启需要在左侧菜单常驻的应用，拖动可调整顺序
          </a-typography-text>
          <div class="category-tabs">
            <a-button
              v-for="cat in categoryOptions"
              :key="cat.key"
              :type="activeCategory === cat.key ? 'primary' : 'default'"
              size="small"
              class="category-btn"
              @click="activeCategory = cat.key"
            >
              {{ cat.label }}
            </a-button>
          </div>
          <div class="app-list" ref="appListRef">
            <div
              v-for="(app, idx) in filteredApps"
              :key="app.route || app.id"
              class="app-row"
              :class="{
                dragging: dragIndex === idx,
                'over-top': dragOverIndex === idx && dragOverDir === 'top',
                'over-bottom': dragOverIndex === idx && dragOverDir === 'bottom',
                disabled: !app.route,
              }"
              :draggable="!!app.route"
              @dragstart="onDragStart(idx, $event)"
              @dragover="onDragOver(idx, $event)"
              @dragend="onDragEnd"
              @drop="onDrop(idx)"
            >
              <div class="app-drag-handle">
                <HolderOutlined />
              </div>
              <div class="app-icon-wrap">
                <component :is="iconMap[app.icon]" />
              </div>
              <div class="app-info">
                <div class="app-name">{{ app.title }}</div>
                <div class="app-desc">{{ app.description }}</div>
              </div>
              <a-switch
                v-if="app.route"
                :checked="appStore.visibleAppRoutes.includes(app.route)"
                size="small"
                @change="appStore.toggleAppRoute(app.route)"
              />
            </div>
          </div>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { HolderOutlined, AuditOutlined, SearchOutlined } from '@ant-design/icons-vue'
import * as Icons from '@ant-design/icons-vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
import { useUserStore } from '@/stores/user'
import { useAppStore } from '@/stores/app'
import { useThemeStore } from '@/stores/theme'
import type { Component } from 'vue'

const userStore = useUserStore()
const appStore = useAppStore()
const themeStore = useThemeStore()

const iconMap: Record<string, Component> = {
  AuditOutlined,
  SearchOutlined,
  FileSearchOutlined: Icons.FileSearchOutlined,
  DashboardOutlined: Icons.DashboardOutlined,
  SafetyOutlined: Icons.SafetyOutlined,
  ApiOutlined: Icons.ApiOutlined,
  EditOutlined: Icons.EditOutlined,
  WarningOutlined: Icons.WarningOutlined,
  FundOutlined: Icons.FundOutlined,
  QuestionCircleOutlined: Icons.QuestionCircleOutlined,
}

const sidebarApps = computed(() => appStore.sidebarApps)

const categoryOptions = [
  { key: 'all', label: '全部' },
  { key: '设计', label: '设计' },
  { key: '施工', label: '施工' },
  { key: '经营', label: '经营' },
]

const activeCategory = ref('all')

const filteredApps = computed(() => {
  const apps = appStore.authorizedApps.filter((a) => a.route)
  if (activeCategory.value === 'all') return apps
  return apps.filter((a) => a.category === activeCategory.value)
})

const dragIndex = ref(-1)
const dragOverIndex = ref(-1)
const dragOverDir = ref<'top' | 'bottom'>('bottom')
const appListRef = ref<HTMLElement>()

function onDragStart(idx: number, e: DragEvent): void {
  dragIndex.value = idx
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move'
}

function onDragOver(idx: number, e: DragEvent): void {
  e.preventDefault()
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
  if (dragIndex.value === -1 || dragIndex.value === idx) {
    onDragEnd()
    return
  }
  const routes = [...appStore.visibleAppRoutes]
  const fromIdx = routes.indexOf(filteredApps.value[dragIndex.value].route!)
  const toIdx = routes.indexOf(filteredApps.value[idx].route!)
  if (fromIdx === -1 || toIdx === -1) {
    onDragEnd()
    return
  }
  const [moved] = routes.splice(fromIdx, 1)
  routes.splice(toIdx, 0, moved)
  appStore.setVisibleRoutes(routes)
  onDragEnd()
}

const preferences = ref({
  language: 'zh-CN',
  notifications: ['business', 'system'],
})

const notifOptions = [
  { label: '业务通知', value: 'business' },
  { label: '系统通知', value: 'system' },
  { label: '审计日志', value: 'audit' },
]
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';

.profile-card {
  margin-bottom: @spacing-md;

  :deep(.section-card-body) {
    padding: @spacing-base;
  }
}

.profile-top-row {
  display: flex;
  align-items: center;
  gap: @spacing-lg;
}

.profile-info {
  min-width: 0;
}

.profile-name {
  font-size: @font-size-xl;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}

.profile-meta {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-top: 2px;

  em {
    margin: 0 @spacing-xs;
    font-style: normal;
    color: @text-tertiary;
  }
}

.profile-desc {
  margin-top: @spacing-base;
  :deep(.ant-descriptions-item) {
    padding-bottom: 0;
  }
}

.profile-bottom {
  margin-top: 0;
  :deep(.section-card-body) {
    padding: @spacing-base;
  }
  :deep(.ant-form-item) {
    margin-bottom: @spacing-sm;
  }
}

.app-desc {
  display: block;
  margin-bottom: @spacing-sm;
}

.category-tabs {
  display: flex;
  gap: @spacing-sm;
  margin-bottom: @spacing-sm;
}

.category-btn {
  min-width: 56px;
}

.app-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
}

.app-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-xs @spacing-sm;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: var(--color-card-bg);
  transition: all 0.15s;
  position: relative;

  &.disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  &:not(.disabled) {
    cursor: default;

    &:hover {
      border-color: var(--color-brand);
      box-shadow: var(--shadow-sm);
    }

    .app-drag-handle {
      cursor: grab;
      color: @text-tertiary;
      &:hover { color: @text-primary; }
    }
  }

  &.dragging {
    opacity: 0.4;
    border-style: dashed;
  }

  &.over-top {
    border-top: 2px solid var(--color-brand);
    margin-top: 2px;
  }

  &.over-bottom {
    border-bottom: 2px solid var(--color-brand);
    margin-bottom: 2px;
  }
}

.app-drag-handle {
  font-size: 14px;
  display: flex;
  align-items: center;
}

.app-icon-wrap {
  width: 36px;
  height: 36px;
  border-radius: @radius-base;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  color: var(--color-brand);
  background: color-mix(in srgb, var(--color-brand) 12%, transparent);
  flex-shrink: 0;
}

.app-info {
  flex: 1;
  min-width: 0;
}

.app-name {
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
}

.app-desc {
  font-size: @font-size-xs;
  color: @text-tertiary;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}


</style>
