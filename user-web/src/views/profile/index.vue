<template>
  <div class="page-container">
    <PageHeader title="个人中心" description="管理个人信息与偏好设置" />

    <a-row :gutter="[24, 24]">
      <a-col :span="8">
        <SectionCard class="profile-card">
          <div class="profile-header">
            <a-avatar :size="72" :style="{ background: '@{brand-gradient}' }">
              {{ userStore.userInfo?.name?.[0] || 'U' }}
            </a-avatar>
            <div class="profile-name">{{ userStore.userInfo?.name || '用户' }}</div>
            <div class="profile-role">{{ userStore.userInfo?.position }}</div>
          </div>
          <a-descriptions :column="1" size="small" class="profile-desc">
            <a-descriptions-item label="部门">{{ userStore.userInfo?.department }}</a-descriptions-item>
            <a-descriptions-item label="邮箱">{{ userStore.userInfo?.email }}</a-descriptions-item>
            <a-descriptions-item label="电话">{{ userStore.userInfo?.phone }}</a-descriptions-item>
          </a-descriptions>
          <div class="scope-section">
            <div class="scope-title">授权范围</div>
            <div class="scope-tags">
              <a-tag v-for="scope in (userStore.userInfo?.authorizedScopes || [])" :key="scope" color="cyan">
                {{ scope }}
              </a-tag>
            </div>
          </div>
        </SectionCard>
      </a-col>

      <a-col :span="16">
        <SectionCard title="偏好设置" class="mb-16">
          <a-form layout="vertical">
            <a-form-item label="界面主题">
              <a-radio-group v-model:value="preferences.theme">
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
            <a-form-item>
              <a-button type="primary">保存设置</a-button>
            </a-form-item>
          </a-form>
        </SectionCard>

        <SectionCard title="最近活动">
          <a-timeline>
            <a-timeline-item v-for="(act, i) in recentActivities" :key="i" :color="act.color">
              <div class="activity-title">{{ act.title }}</div>
              <div class="activity-time">{{ act.time }}</div>
            </a-timeline-item>
          </a-timeline>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import SectionCard from '@/components/SectionCard.vue'
import { useUserStore } from '@/stores/user'

const userStore = useUserStore()

const preferences = ref({
  theme: 'light',
  language: 'zh-CN',
  notifications: ['business', 'system'],
})

const notifOptions = [
  { label: '业务通知', value: 'business' },
  { label: '系统通知', value: 'system' },
  { label: '审计日志', value: 'audit' },
]

const recentActivities = [
  { title: '完成 AI 审标任务：XX 项目招标文件风险分析', time: '2026-07-17 14:35', color: 'green' },
  { title: '查询标准：GB/T 19001 质量管理体系', time: '2026-07-17 10:15', color: 'blue' },
  { title: '上传文件：XX_项目_招标文件.pdf', time: '2026-07-17 14:30', color: 'gray' },
  { title: '生成合同审查报告：供应商协议 v3', time: '2026-07-15 11:25', color: 'green' },
  { title: '登录系统', time: '2026-07-15 08:50', color: 'gray' },
]
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.mb-16 { margin-bottom: @spacing-lg; }

.profile-card { text-align: center; }
.profile-header {
  padding: @spacing-lg 0;
  display: flex; flex-direction: column; align-items: center; gap: @spacing-sm;
}
.profile-name {
  font-size: @font-size-xl; font-weight: @font-weight-semibold; color: @text-primary;
}
.profile-role { font-size: @font-size-sm; color: @text-secondary; }
.profile-desc { text-align: left; margin: @spacing-lg 0; }
.scope-section { border-top: 1px solid @divider-color; padding-top: @spacing-lg; text-align: left; }
.scope-title { font-size: @font-size-sm; color: @text-secondary; margin-bottom: @spacing-sm; }
.scope-tags { display: flex; flex-wrap: wrap; gap: @spacing-xs; }

.activity-title { font-size: @font-size-sm; color: @text-primary; }
.activity-time { font-size: @font-size-xs; color: @text-tertiary; margin-top: 2px; }
</style>
