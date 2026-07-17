<template>
  <a-layout style="min-height: 100vh">
    <a-layout-sider v-model:collapsed="collapsed" :trigger="null" collapsible theme="dark" width="220">
      <div class="logo">
        <span v-if="!collapsed" class="logo-text">智浚AI</span>
        <span v-if="!collapsed" class="logo-sub">DredgeAI</span>
        <span v-else class="logo-mini">智</span>
      </div>
      <a-menu v-model:selectedKeys="selectedKeys" theme="dark" mode="inline" @click="handleMenuClick">
        <a-menu-item key="/admin/dashboard">
          <DashboardOutlined />
          <span>管理工作台</span>
        </a-menu-item>
        <a-menu-item key="/admin/permissions">
          <SafetyOutlined />
          <span>权限管理</span>
        </a-menu-item>
        <a-menu-item key="/admin/applications">
          <AppstoreOutlined />
          <span>应用管理</span>
        </a-menu-item>
        <a-menu-item key="/admin/data">
          <DatabaseOutlined />
          <span>数据治理</span>
        </a-menu-item>
        <a-menu-item key="/admin/analytics">
          <BarChartOutlined />
          <span>统计分析</span>
        </a-menu-item>
      </a-menu>
    </a-layout-sider>
    <a-layout>
      <a-layout-header class="admin-header">
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
          <a-tag color="#00c9b7" class="admin-tag">管理后台</a-tag>
          <a-badge count="5" :offset="[2, -2]">
            <BellOutlined class="header-icon" />
          </a-badge>
          <a-dropdown>
            <span class="user-info">
              <a-avatar size="small" style="backgroundColor: #1a2332">A</a-avatar>
              <span class="user-name">管理员</span>
            </span>
            <template #overlay>
              <a-menu>
                <a-menu-item key="logout"><LogoutOutlined /> 退出登录</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </a-layout-header>
      <a-layout-content class="admin-content">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, SafetyOutlined, AppstoreOutlined, DatabaseOutlined,
  BarChartOutlined, MenuUnfoldOutlined, MenuFoldOutlined,
  BellOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'

const router = useRouter()
const route = useRoute()

const collapsed = ref(false)
const selectedKeys = ref<string[]>([route.path])
const currentRole = ref(route.path.startsWith('/admin') ? 'admin' : 'user')

const roleOptions = [
  { label: '管理后台', value: 'admin' },
  { label: '返回用户端', value: 'user' },
]

function handleRoleChange(value: string | number) {
  const v = value as 'user' | 'admin'
  currentRole.value = v
  router.push(v === 'admin' ? '/admin/dashboard' : '/user/dashboard')
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

.admin-header {
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
.admin-tag {
  font-size: 12px;
  border-radius: 4px;
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

.admin-content {
  min-height: calc(100vh - @header-height);
}
</style>
