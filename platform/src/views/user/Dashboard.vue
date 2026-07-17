<template>
  <div class="page-container">
    <div class="welcome-section">
      <div class="welcome-text">
        <h2>欢迎回来，{{ store.user.name }}</h2>
        <p>{{ store.user.department }} · {{ store.user.position }}</p>
      </div>
      <a-input-search placeholder="搜索应用、任务或标准..." style="width: 360px" />
    </div>

    <a-row :gutter="[24, 24]">
      <a-col :span="16">
        <a-card class="dashboard-card">
          <template #title><span class="card-title">推荐任务</span></template>
          <a-list item-layout="horizontal" :data-source="store.tasks">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #title>
                    <span>{{ item.title }}</span>
                  </template>
                  <template #description>
                    <span>更新于 {{ item.updatedAt }}</span>
                  </template>
                </a-list-item-meta>
                <template #extra>
                  <a-tag :color="item.status === '进行中' ? 'processing' : 'success'">{{ item.status }}</a-tag>
                  <a-button type="link" size="small">继续</a-button>
                </template>
              </a-list-item>
            </template>
          </a-list>
        </a-card>

        <a-card class="dashboard-card" style="margin-top: 24px">
          <template #title><span class="card-title">授权应用</span></template>
          <a-row :gutter="[16, 16]">
            <a-col v-for="app in authorizedApps" :key="app.id" :span="8">
              <a-card :class="['app-mini-card', { 'has-hover': true }]" size="small" hoverable>
                <a-card-meta>
                  <template #title>
                    <span style="font-size: 13px">{{ app.title }}</span>
                  </template>
                  <template #description>
                    <span style="font-size: 12px; color: #999">{{ app.description }}</span>
                  </template>
                </a-card-meta>
              </a-card>
            </a-col>
          </a-row>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card class="dashboard-card">
          <template #title><span class="card-title">最近文件</span></template>
          <a-list item-layout="horizontal" :data-source="store.files">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-list-item-meta>
                  <template #title>
                    <span style="font-size: 13px">{{ item.name }}</span>
                  </template>
                  <template #description>
                    <span style="font-size: 12px; color: #999">{{ item.updatedAt }}</span>
                  </template>
                </a-list-item-meta>
              </a-list-item>
            </template>
          </a-list>
        </a-card>

        <a-card class="dashboard-card" style="margin-top: 24px">
          <template #title><span class="card-title">个人效率</span></template>
          <div class="efficiency-stats">
            <div class="stat-item">
              <div class="stat-label">本月任务</div>
              <div class="stat-number">24</div>
            </div>
            <div class="stat-item">
              <div class="stat-label">完成率</div>
              <div class="stat-number">83%</div>
            </div>
            <div class="stat-item">
              <div class="stat-label">平均时长</div>
              <div class="stat-number">12min</div>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useAppStore } from '@/stores/app'

const store = useAppStore()
const authorizedApps = computed(() => store.apps.filter(a => a.status === '已授权'))
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.welcome-section {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  h2 {
    font-size: 22px;
    font-weight: 600;
    margin-bottom: 4px;
  }
  p {
    color: @text-secondary;
    font-size: 14px;
  }
}

.dashboard-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}

.app-mini-card {
  border-radius: @border-radius;
  transition: all 0.2s;
  &.has-hover:hover {
    box-shadow: @shadow-md;
    border-color: @accent-color;
  }
}

.efficiency-stats {
  display: flex;
  justify-content: space-around;
  text-align: center;
}
.stat-item {
  padding: 8px 0;
}
.stat-label {
  font-size: 12px;
  color: @text-secondary;
  margin-bottom: 4px;
}
.stat-number {
  font-size: 22px;
  font-weight: 700;
  color: @text-primary;
}
</style>
