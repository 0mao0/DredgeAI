<template>
  <div class="page-container">
    <PageHeader title="应用广场" description="按场景筛选 AI 应用，快速进入工作流">
      <template #extra>
        <SearchInput v-model="searchKeyword" placeholder="搜索应用名称" class="app-search" />
      </template>
    </PageHeader>

    <div class="filter-bar">
      <a-segmented v-model:value="activeCategory" :options="categoryOptions" @change="handleCategoryChange" />
    </div>

    <a-row :gutter="[20, 20]">
      <a-col v-for="app in filteredApps" :key="app.id" :span="8">
        <div class="app-card" :class="{ 'app-card--disabled': app.status === '待申请' }">
          <div class="app-card-header">
            <div class="app-card-icon">
              <component :is="iconMap[app.icon]" />
            </div>
            <div class="app-card-status">
              <StatusTag :status="app.status" />
            </div>
          </div>
          <div class="app-card-body">
            <h3 class="app-card-title">{{ app.title }}</h3>
            <p class="app-card-desc">{{ app.description }}</p>
            <div class="app-card-meta">
              <a-tag size="small">{{ app.category }}</a-tag>
              <span v-if="app.version" class="app-card-version">{{ app.version }}</span>
            </div>
          </div>
          <div class="app-card-footer">
            <a-button
              v-if="app.status === '已授权'"
              type="primary"
              block
              @click="handleEnterApp(app)"
            >
              进入应用
            </a-button>
            <a-button
              v-else-if="app.status === '待申请'"
              block
              @click="handleApplyApp(app)"
            >
              申请权限
            </a-button>
            <a-button v-else block disabled>已下架</a-button>
          </div>
        </div>
      </a-col>
    </a-row>

    <EmptyState v-if="filteredApps.length === 0" description="没有匹配的应用" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  CodeOutlined, TeamOutlined,
} from '@ant-design/icons-vue'
import PageHeader from '@/components/PageHeader.vue'
import SearchInput from '@/components/SearchInput.vue'
import StatusTag from '@/components/StatusTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useAppStore } from '@/stores/app'
import type { AppCard } from '@/types'
import type { Component } from 'vue'

const router = useRouter()
const appStore = useAppStore()

const searchKeyword = ref('')
const activeCategory = ref('all')

const iconMap: Record<string, Component> = {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  CodeOutlined, TeamOutlined,
}

const categoryOptions = computed(() => {
  const opts = [{ label: '全部', value: 'all' }]
  return opts.concat((appStore.categories as { key: string; label: string }[])
    .filter((c) => c.key !== 'all')
    .map((c) => ({ label: c.label, value: c.key })))
})

const filteredApps = computed(() => {
  let list = appStore.apps
  if (activeCategory.value !== 'all') {
    list = list.filter((a) => a.category === activeCategory.value)
  }
  if (searchKeyword.value) {
    const kw = searchKeyword.value.toLowerCase()
    list = list.filter((a) => a.title.toLowerCase().includes(kw) || a.description.toLowerCase().includes(kw))
  }
  return list
})

function handleCategoryChange(): void {}

function handleEnterApp(app: AppCard): void {
  if (app.route) {
    router.push(app.route)
  } else {
    message.info(`${app.title} 暂未开放`)
  }
}

function handleApplyApp(app: AppCard): void {
  message.success(`已提交 ${app.title} 的权限申请`)
}

onMounted(async () => {
  await Promise.all([appStore.fetchApps(), appStore.fetchCategories()])
})
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.app-search { width: 280px; }

.filter-bar {
  margin-bottom: @spacing-xl;
}

.app-card {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  padding: @spacing-xl;
  transition: all @transition-base;
  height: 100%;
  display: flex;
  flex-direction: column;
  &:hover {
    box-shadow: @shadow-md;
    transform: translateY(-2px);
    border-color: color-mix(in srgb, @brand-primary 30%, transparent);
  }
  &--disabled { opacity: 0.7; }
}
.app-card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: @spacing-md;
}
.app-card-icon {
  width: 48px; height: 48px;
  border-radius: @radius-base;
  background: @brand-gradient;
  color: white;
  display: flex; align-items: center; justify-content: center;
  font-size: 22px;
  box-shadow: @shadow-brand;
}
.app-card-title {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  margin-bottom: @spacing-xs;
}
.app-card-desc {
  font-size: @font-size-sm;
  color: @text-secondary;
  line-height: 1.5;
  margin-bottom: @spacing-md;
  min-height: 42px;
}
.app-card-meta {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-bottom: @spacing-lg;
}
.app-card-version {
  font-size: @font-size-xs;
  color: @text-tertiary;
}
.app-card-footer { margin-top: auto; }
</style>
