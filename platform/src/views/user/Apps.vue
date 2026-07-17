<template>
  <div class="page-container">
    <div class="page-header">
      <h2>应用广场</h2>
      <p>按场景筛选 AI 应用，快速进入</p>
    </div>

    <a-card class="filter-card" :body-style="{ padding: '12px 24px' }">
      <a-space wrap>
        <a-button
          v-for="cat in categories"
          :key="cat.key"
          :type="activeCategory === cat.key ? 'primary' : 'default'"
          size="small"
          @click="activeCategory = cat.key"
        >
          {{ cat.label }}
        </a-button>
      </a-space>
    </a-card>

    <a-row :gutter="[24, 24]" style="margin-top: 24px">
      <a-col v-for="app in filteredApps" :key="app.id" :xs="24" :sm="12" :lg="8" :xl="6">
        <a-card hoverable class="app-card">
          <div class="app-icon">
            <component :is="getIcon(app.icon)" :style="{ fontSize: '32px', color: '#00c9b7' }" />
          </div>
          <a-card-meta>
            <template #title>
              <div class="app-title">
                <span>{{ app.title }}</span>
                <a-tag v-if="app.status === '待申请'" color="default" style="font-size: 11px">待申请</a-tag>
                <a-tag v-else color="#00c9b7" style="font-size: 11px">已授权</a-tag>
              </div>
            </template>
            <template #description>
              <span style="font-size: 13px; color: #999">{{ app.description }}</span>
              <div class="app-category-tag">{{ app.category }}</div>
            </template>
          </a-card-meta>
          <template #actions>
            <a-button v-if="app.status === '已授权'" type="primary" size="small" block>进入应用</a-button>
            <a-button v-else size="small" block>申请授权</a-button>
          </template>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAppStore } from '@/stores/app'
import { categories } from '@/mock/data'
import {
  FileSearchOutlined, BookOutlined, EditOutlined, SafetyOutlined,
  DashboardOutlined, ApiOutlined, QuestionCircleOutlined, SwapOutlined,
  AppstoreOutlined,
} from '@ant-design/icons-vue'

const store = useAppStore()
const activeCategory = ref('all')

const filteredApps = computed(() => {
  if (activeCategory.value === 'all') return store.apps
  return store.apps.filter(a => a.category === activeCategory.value)
})

function getIcon(icon?: string) {
  const map: Record<string, any> = {
    FileSearch: FileSearchOutlined, Book: BookOutlined, Edit: EditOutlined,
    Safety: SafetyOutlined, Dashboard: DashboardOutlined, Api: ApiOutlined,
    QuestionCircle: QuestionCircleOutlined, Swap: SwapOutlined,
  }
  return icon && map[icon] ? map[icon] : AppstoreOutlined
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.filter-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}

.app-card {
  border-radius: @border-radius;
  transition: all 0.2s;
  &:hover { box-shadow: @shadow-md; }
}

.app-icon {
  text-align: center;
  margin-bottom: 16px;
}

.app-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 15px;
  font-weight: 600;
}

.app-category-tag {
  display: inline-block;
  margin-top: 8px;
  font-size: 11px;
  color: @accent-color;
  background: rgba(0, 201, 183, 0.1);
  padding: 2px 8px;
  border-radius: 4px;
}
</style>
