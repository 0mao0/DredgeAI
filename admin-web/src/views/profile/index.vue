<template>
  <div class="page-container">
    <PageHeader title="个人中心" description="管理个人信息与账号设置">
      <template #extra>
        <a-space>
          <a-popconfirm title="确定退出登录？" ok-text="退出" cancel-text="取消" @confirm="authStore.logout()">
            <AppButton danger>退出登录</AppButton>
          </a-popconfirm>
          <AppButton variant="primary" :disabled="!profile" @click="openEditModal">编辑资料</AppButton>
        </a-space>
      </template>
    </PageHeader>

    <a-skeleton v-if="!profile" active :paragraph="{ rows: 6 }" />

    <a-row v-else :gutter="[24, 24]">
      <a-col :span="8">
        <a-card class="profile-card">
          <div class="profile-avatar-wrap">
            <a-avatar :size="80" :style="{ background: 'var(--color-brand)', fontSize: '32px' }">{{ profile.name?.[0] || 'A' }}</a-avatar>
            <div class="profile-name">{{ profile.name }}</div>
            <div class="profile-role">{{ roleLabel }}</div>
          </div>
        </a-card>
      </a-col>
      <a-col :span="16">
        <SectionCard title="基本信息">
          <a-descriptions :column="2" bordered :label-style="{ width: '120px' }">
            <a-descriptions-item label="用户名">{{ profile.username }}</a-descriptions-item>
            <a-descriptions-item label="姓名">{{ profile.name }}</a-descriptions-item>
            <a-descriptions-item label="邮箱">{{ profile.email }}</a-descriptions-item>
            <a-descriptions-item label="手机">{{ profile.phone || '—' }}</a-descriptions-item>
            <a-descriptions-item label="部门">{{ departmentLabel }}</a-descriptions-item>
            <a-descriptions-item label="角色">{{ roleLabel }}</a-descriptions-item>
            <a-descriptions-item label="注册时间">{{ formatTime(profile.createdAt) }}</a-descriptions-item>
            <a-descriptions-item label="最后登录">{{ formatTime(profile.lastLogin) }}</a-descriptions-item>
          </a-descriptions>
        </SectionCard>
      </a-col>
    </a-row>

    <a-modal
      v-model:open="editOpen"
      title="编辑资料"
      :width="440"
      :confirm-loading="saving"
      @ok="submitEdit"
    >
      <a-form ref="formRef" :model="editForm" :rules="formRules" layout="vertical">
        <a-form-item label="姓名" name="name">
          <a-input v-model:value="editForm.name" placeholder="请输入姓名" />
        </a-form-item>
        <a-form-item label="邮箱" name="email">
          <a-input v-model:value="editForm.email" placeholder="请输入邮箱" />
        </a-form-item>
        <a-form-item label="手机" name="phone">
          <a-input v-model:value="editForm.phone" placeholder="请输入手机号" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { useAppStore } from '@/stores/app'
import { useAuthStore } from '@/stores/auth'
import { getProfile, updateProfile } from '@/api/modules/profile'

const appStore = useAppStore()
const authStore = useAuthStore()
const profile = computed(() => appStore.profile)

const roleLabel = computed(() => {
  const roles = profile.value?.roles ?? []
  if (!roles.length) return '—'
  return roles.map((r) => ({ admin: '管理员' })[r] ?? r).join('、')
})

const departmentLabel = computed(() => {
  const departments = profile.value?.departments ?? []
  return departments.length ? departments.join('、') : '—'
})

function formatTime(iso?: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const pad = (n: number): string => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

const editOpen = ref(false)
const saving = ref(false)
const formRef = ref<FormInstance>()
const editForm = reactive({ name: '', email: '', phone: '' })

const formRules = {
  name: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email' as const, message: '邮箱格式不正确', trigger: 'blur' },
  ],
}

function openEditModal(): void {
  if (!profile.value) return
  editForm.name = profile.value.name
  editForm.email = profile.value.email
  editForm.phone = profile.value.phone ?? ''
  editOpen.value = true
}

async function submitEdit(): Promise<void> {
  if (!profile.value) return
  await formRef.value?.validate()
  saving.value = true
  try {
    const result = await updateProfile({
      userName: profile.value.username,
      email: editForm.email,
      name: editForm.name,
      phoneNumber: editForm.phone || undefined,
      concurrencyStamp: profile.value.concurrencyStamp,
    })
    appStore.setProfile(result)
    message.success('保存成功')
    editOpen.value = false
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  if (!appStore.profile) {
    appStore.setProfile(await getProfile())
  }
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';
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
