<template>
  <a-modal
    :open="open"
    :title="editing ? '编辑路由' : '新增路由'"
    :width="640"
    :confirm-loading="saving"
    @ok="handleOk"
    @cancel="emit('update:open', false)"
  >
    <a-form ref="formRef" :model="form" :rules="rules" layout="vertical">
      <a-form-item label="路由 ID" name="routeId">
        <a-input v-model:value="form.routeId" :maxlength="128" placeholder="如 bidcompare-api" />
      </a-form-item>
      <a-form-item label="描述" name="description">
        <a-textarea v-model:value="form.description" :maxlength="256" :rows="2" placeholder="可选" />
      </a-form-item>
      <a-form-item label="目标集群" name="clusterId">
        <a-select
          v-model:value="form.clusterId"
          :options="clusterOptions"
          placeholder="选择目标集群"
          show-search
          option-filter-prop="label"
        />
      </a-form-item>
      <a-form-item label="匹配路径" name="matchPath">
        <a-input v-model:value="form.matchPath" placeholder="/api/bidcompare/{**catch-all}" />
      </a-form-item>
      <a-form-item label="匹配 Hosts" name="matchHosts">
        <a-select
          v-model:value="form.matchHosts"
          mode="tags"
          :options="[]"
          placeholder="域名，可多个，留空匹配全部"
        />
      </a-form-item>
      <a-form-item label="优先级" name="order">
        <div class="route-form__order">
          <a-input-number v-model:value="form.order" :min="0" />
          <span class="route-form__order-hint">数值越小越先匹配</span>
        </div>
      </a-form-item>
      <a-form-item label="授权策略" name="authorizationPolicy">
        <a-select
          v-model:value="form.authorizationPolicy"
          :options="POLICY_OPTIONS"
          allow-clear
          placeholder="留空则不做授权限制"
        />
        <div v-if="selectedPolicyDescription" class="route-form__policy-desc">
          {{ selectedPolicyDescription }}
        </div>
      </a-form-item>
      <a-form-item label="启用" name="isEnabled">
        <a-switch v-model:checked="form.isEnabled" />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance } from 'ant-design-vue'
import type { ProxyRouteFormData, ProxyRouteItem } from '@/api/modules/gateway'

const props = defineProps<{
  open: boolean
  saving: boolean
  editing: ProxyRouteItem | null
  clusterOptions: Array<{ value: string, label: string }>
}>()

const emit = defineEmits<{
  'update:open': [open: boolean]
  'submit': [data: ProxyRouteFormData]
}>()

const formRef = ref<FormInstance>()
const form = reactive({
  routeId: '',
  description: '',
  clusterId: undefined as string | undefined,
  matchPath: '',
  matchHosts: [] as string[],
  order: 0,
  authorizationPolicy: undefined as string | undefined,
  isEnabled: true,
})

/** YARP 内置授权策略（与 Yarp.ReverseProxy 约定值一致） */
const POLICY_OPTIONS = [
  { value: 'anonymous', label: 'anonymous' },
  { value: 'default', label: 'default' },
]

const POLICY_DESCRIPTIONS: Record<string, string> = {
  anonymous: '匿名访问：允许未登录请求通过该路由（YARP AnonymousAuthorizationPolicy）',
  default: '默认策略：要求请求已完成认证（YARP DefaultAuthorizationPolicy，未登录返回 401/403）',
}

const selectedPolicyDescription = computed(() =>
  form.authorizationPolicy ? POLICY_DESCRIPTIONS[form.authorizationPolicy] : undefined,
)

const rules = {
  routeId: [{ required: true, message: '请输入路由 ID', trigger: 'blur' }],
  clusterId: [{ required: true, message: '请选择目标集群', trigger: 'change' }],
  matchPath: [{ required: true, message: '请输入匹配路径', trigger: 'blur' }],
}

watch(() => props.open, (open) => {
  if (!open) return
  const r = props.editing
  form.routeId = r?.routeId ?? ''
  form.description = r?.description ?? ''
  form.clusterId = r?.clusterId ?? undefined
  form.matchPath = r?.match.path ?? ''
  form.matchHosts = [...(r?.match.hosts ?? [])]
  form.order = r?.order ?? 0
  form.authorizationPolicy = r?.authorizationPolicy ?? undefined
  form.isEnabled = r?.isEnabled ?? true
  formRef.value?.clearValidate()
})

async function handleOk(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  emit('submit', {
    routeId: form.routeId.trim(),
    description: form.description.trim() || undefined,
    clusterId: form.clusterId!,
    authorizationPolicy: form.authorizationPolicy || undefined,
    order: form.order ?? 0,
    match: {
      path: form.matchPath.trim(),
      hosts: form.matchHosts.length ? form.matchHosts : undefined,
    },
    isEnabled: form.isEnabled,
  })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.route-form__order {
  display: flex;
  align-items: center;
  gap: @spacing-sm;

  &-hint {
    color: @text-tertiary;
    font-size: @font-size-xs;
  }
}

.route-form__policy-desc {
  margin-top: 4px;
  color: @text-tertiary;
  font-size: @font-size-xs;
  line-height: 1.5;
}
</style>
