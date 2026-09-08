<template>
  <div class="auth-callback">
    <!-- 处理中：正在用 code + PKCE verifier 换 token -->
    <a-spin v-if="status === 'processing'" size="large" tip="正在完成登录…">
      <div class="auth-callback__spin-space" />
    </a-spin>

    <!-- 失败：展示原因并引导返回登录页 -->
    <a-result
      v-else
      status="error"
      title="登录失败"
      :sub-title="errorMessage"
      class="auth-callback__result"
    >
      <template #extra>
        <AppButton variant="primary" @click="backToLogin">返回登录</AppButton>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { AppButton } from '@shared/web'
import { OAUTH_STATE_KEY, useAuthStore } from '@/stores/auth'
import { LOGIN_PATH } from '@/utils/constants'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

type CallbackStatus = 'processing' | 'error'
const status = ref<CallbackStatus>('processing')
const errorMessage = ref('')

function readQueryString(raw: unknown): string | null {
  return typeof raw === 'string' && raw.length > 0 ? raw : null
}

function fail(reason: string): void {
  errorMessage.value = reason
  status.value = 'error'
}

function backToLogin(): void {
  void router.replace(LOGIN_PATH)
}

onMounted(() => {
  const code = readQueryString(route.query.code)
  const state = readQueryString(route.query.state)
  const error = readQueryString(route.query.error)
  const errorDescription = readQueryString(route.query.error_description)

  // 认证中心侧拒绝 / 出错（如用户取消授权）：中间态作废
  if (error) {
    sessionStorage.removeItem(OAUTH_STATE_KEY)
    fail(errorDescription ?? '认证中心拒绝了本次登录，请重试')
    return
  }

  // state 校验（防 CSRF：须与发起登录时写入 sessionStorage 的值一致）
  const expectedState = sessionStorage.getItem(OAUTH_STATE_KEY)
  if (!code || !state || !expectedState || state !== expectedState) {
    sessionStorage.removeItem(OAUTH_STATE_KEY)
    fail('登录校验未通过（缺少或非法的回调参数），请重新发起登录')
    return
  }

  void completeLogin(code)
})

async function completeLogin(code: string): Promise<void> {
  try {
    // 内部用 sessionStorage 中的 PKCE verifier 换 token，中间态用完即清除
    await authStore.completeOidc(code)
    await router.replace('/')
  } catch (e) {
    fail(e instanceof Error && e.message ? e.message : '登录失败，请重新发起登录')
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.auth-callback {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: @page-padding;
  background: @content-bg;
}

.auth-callback__spin-space {
  min-height: 120px;
}

.auth-callback__result {
  width: 100%;
  max-width: 480px;
}
</style>
