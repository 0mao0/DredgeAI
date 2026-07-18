<template>
  <div class="page-container">
    <PageHeader title="个人中心" description="管理个人信息与账号设置" />

    <a-row :gutter="[24, 24]">
      <a-col :span="8">
        <a-card class="profile-card">
          <div class="profile-avatar-wrap">
            <a-avatar :size="80" :style="{ background: '#0EA5E9', fontSize: '32px' }">{{ profile?.name?.[0] || 'A' }}</a-avatar>
            <div class="profile-name">{{ profile?.name || '管理员' }}</div>
            <div class="profile-role">{{ roleLabel }}</div>
          </div>
        </a-card>
      </a-col>
      <a-col :span="16">
        <SectionCard title="基本信息">
          <a-descriptions :column="2" bordered :label-style="{ width: '120px' }">
            <a-descriptions-item label="用户名">{{ profile?.username }}</a-descriptions-item>
            <a-descriptions-item label="姓名">{{ profile?.name }}</a-descriptions-item>
            <a-descriptions-item label="邮箱">{{ profile?.email }}</a-descriptions-item>
            <a-descriptions-item label="手机">{{ profile?.phone }}</a-descriptions-item>
            <a-descriptions-item label="部门">{{ profile?.department }}</a-descriptions-item>
            <a-descriptions-item label="角色">{{ roleLabel }}</a-descriptions-item>
            <a-descriptions-item label="注册时间">{{ profile?.createdAt }}</a-descriptions-item>
            <a-descriptions-item label="最后登录">{{ profile?.lastLogin }}</a-descriptions-item>
          </a-descriptions>
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
import { useAppStore } from '@/stores/app'
import { getProfile } from '@/api/modules/profile'

const appStore = useAppStore()
const profile = computed(() => appStore.profile)

const roleLabel = computed(() => {
  const map: Record<string, string> = { super_admin: '超级管理员', admin: '管理员', operator: '操作员' }
  return map[profile.value?.role || ''] || '未知'
})

onMounted(async () => {
  if (!appStore.profile) {
    const user = await getProfile()
    appStore.setProfile(user)
  }
})
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';
.profile-card { text-align: center; }
.profile-avatar-wrap {
  padding: @spacing-xl 0;
}
.profile-name {
  font-size: @font-size-xl;
  font-weight: @font-weight-semibold;
  margin-top: @spacing-md;
}
.profile-role {
  font-size: @font-size-sm;
  color: @text-secondary;
  margin-top: @spacing-xs;
}
</style>
