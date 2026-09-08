<template>
  <div class="login-page">
    <div class="login-page__card">
      <div class="login-page__brand">
        <Logo class="login-page__brand-logo" collapsed />
        <h1 class="login-page__brand-title">智浚AI</h1>
      </div>

      <a-form
        ref="formRef"
        layout="vertical"
        :model="formState"
        :rules="formRules"
        @finish="handleSubmit"
      >
        <a-form-item label="账号" name="username">
          <a-input
            v-model:value="formState.username"
            size="large"
            placeholder="请输入账号"
            autocomplete="username"
            allow-clear
          />
        </a-form-item>
        <a-form-item label="密码" name="password">
          <a-input-password
            v-model:value="formState.password"
            size="large"
            placeholder="请输入密码"
            autocomplete="current-password"
          />
        </a-form-item>
        <AppButton
          class="login-page__submit"
          variant="primary"
          size="lg"
          block
          html-type="submit"
          :loading="submitting"
        >
          {{ submitting ? '登录中…' : '登 录' }}
        </AppButton>
      </a-form>

      <div class="login-page__divider" />

      <div class="login-page__alt">
        <AppButton class="login-page__alt-btn" @click="startAuthCenter">
          Auth 认证中心登录
        </AppButton>
        <AppButton class="login-page__alt-btn" @click="qrVisible = true">
          交建通扫码登录
        </AppButton>
      </div>
    </div>

    <!-- 交建通扫码：后端暂无接口，占位弹框（用户已确认） -->
    <a-modal
      v-model:open="qrVisible"
      title="交建通扫码登录"
      :width="440"
      :footer="null"
      centered
    >
      <div class="login-page__qr-placeholder">
        <QrcodeOutlined />
      </div>
      <p class="login-page__qr-tip">交建通扫码登录功能建设中，敬请期待</p>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { QrcodeOutlined } from '@ant-design/icons-vue'
import { AppButton, Logo } from '@shared/web'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const formRef = ref<FormInstance>()
const submitting = ref(false)
const qrVisible = ref(false)

const formState = reactive({ username: '', password: '' })
const formRules: Record<string, Rule[]> = {
  username: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

/** 登录成功后的落地页：仅放行站内路径，防开放重定向 */
function resolveRedirect(): string {
  const raw = typeof route.query.redirect === 'string' ? route.query.redirect : ''
  return raw.startsWith('/') && !raw.startsWith('//') ? raw : '/'
}

function startAuthCenter(): void {
  authStore.startAuthCenterLogin()
}

async function handleSubmit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    await authStore.login(formState.username.trim(), formState.password)
    await router.replace(resolveRedirect())
  } catch (e) {
    // 后端 /connect/token 的 error_description（ABP 已本地化），非 Error 时给兜底文案
    message.error(e instanceof Error ? e.message : '登录失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: @page-padding;
  overflow: hidden;
  // 品牌色柔光氛围（纯 CSS 变量，随明暗主题自动切换）
  background:
    radial-gradient(720px 420px at 12% 8%, color-mix(in srgb, @brand-primary 10%, transparent), transparent 70%),
    radial-gradient(640px 380px at 88% 92%, color-mix(in srgb, @accent 10%, transparent), transparent 70%),
    @content-bg;
}

.login-page__card {
  width: 100%;
  max-width: 400px;
  padding: @spacing-4xl @spacing-3xl @spacing-2xl;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-xl;
  box-shadow: @shadow-lg;
  animation: login-card-in @transition-slow both;
}

@keyframes login-card-in {
  from {
    opacity: 0;
    transform: translateY(@spacing-base);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .login-page__card {
    animation: none;
  }
}

.login-page__brand {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-bottom: @spacing-2xl;
}

// Logo 组件（scoped 下作用于组件根元素）：仅取标识图，压缩侧栏场景的高度/留白
.login-page__brand .login-page__brand-logo {
  height: auto;
  padding: 0;
  margin-bottom: @spacing-sm;
}

.login-page__brand-title {
  margin: 0;
  color: @text-primary;
  font-size: @font-size-2xl;
  font-weight: @font-weight-semibold;
  letter-spacing: 0.03em;
  line-height: 1.3;
}

.login-page__submit {
  margin-top: @spacing-xs;
}

.login-page__divider {
  height: 1px;
  margin: @spacing-xl 0 @spacing-lg;
  background: @divider-color;
}

.login-page__alt {
  display: flex;
  gap: @spacing-md;
}

.login-page__alt-btn {
  flex: 1;
}

.login-page__qr-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 200px;
  height: 200px;
  margin: 0 auto;
  color: @text-tertiary;
  font-size: 64px;
  border: 1px dashed @border-color;
  border-radius: @radius-lg;
}

.login-page__qr-tip {
  margin: @spacing-lg 0 0;
  text-align: center;
  color: @text-secondary;
  font-size: @font-size-base;
}
</style>
