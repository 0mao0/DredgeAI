<template>
  <a-modal
    :open="open"
    :title="editing ? '编辑策略' : '新增策略'"
    :width="640"
    :confirm-loading="saving"
    @ok="handleOk"
    @cancel="emit('update:open', false)"
  >
    <a-form ref="formRef" :model="form" :rules="rules" layout="vertical">
      <a-form-item label="策略名称" name="name">
        <a-input v-model:value="form.name" :maxlength="128" placeholder="如 global-default" />
      </a-form-item>
      <a-form-item label="作用域" name="scope">
        <a-radio-group v-model:value="form.scope" option-type="button" size="small">
          <a-radio-button :value="0" :disabled="globalOptionDisabled">全局</a-radio-button>
          <a-radio-button :value="1">路由级</a-radio-button>
        </a-radio-group>
        <div v-if="globalOptionDisabled" class="policy-form__hint">已存在全局策略，仅允许一条</div>
      </a-form-item>
      <a-form-item v-if="form.scope === 1" label="目标路由" name="routeId">
        <a-select
          v-model:value="form.routeId"
          :options="routeSelectOptions"
          placeholder="选择目标路由"
          show-search
          option-filter-prop="label"
          allow-clear
        />
      </a-form-item>
      <a-form-item label="限流算法" name="algorithm">
        <a-select
          v-model:value="form.algorithm"
          :options="ALGORITHM_OPTIONS"
          placeholder="选择限流算法"
          @change="handleAlgorithmChange"
        />
      </a-form-item>
      <template v-if="form.algorithm === 0 || form.algorithm === 1">
        <div class="policy-form__row">
          <a-form-item label="窗口内许可数" name="permitLimit" class="policy-form__row-item">
            <a-input-number v-model:value="form.permitLimit" :min="1" placeholder="如 100" />
          </a-form-item>
          <a-form-item label="窗口时长（秒）" name="windowSeconds" class="policy-form__row-item">
            <a-input-number v-model:value="form.windowSeconds" :min="1" placeholder="如 10" />
          </a-form-item>
        </div>
      </template>
      <a-form-item v-if="form.algorithm === 1" label="每窗口分段数" name="segmentsPerWindow">
        <a-input-number v-model:value="form.segmentsPerWindow" :min="1" placeholder="如 6" />
      </a-form-item>
      <template v-if="form.algorithm === 2">
        <div class="policy-form__row">
          <a-form-item label="令牌桶容量" name="tokenLimit" class="policy-form__row-item">
            <a-input-number v-model:value="form.tokenLimit" :min="1" placeholder="如 100" />
          </a-form-item>
          <a-form-item label="每周期补充令牌数" name="tokensPerPeriod" class="policy-form__row-item">
            <a-input-number v-model:value="form.tokensPerPeriod" :min="1" placeholder="如 20" />
          </a-form-item>
          <a-form-item label="补充周期（秒）" name="replenishmentPeriodSeconds" class="policy-form__row-item">
            <a-input-number v-model:value="form.replenishmentPeriodSeconds" :min="1" placeholder="如 10" />
          </a-form-item>
        </div>
      </template>
      <a-form-item label="排队上限" name="queueLimit">
        <a-input-number v-model:value="form.queueLimit" :min="0" />
      </a-form-item>
      <a-form-item label="启用" name="isEnabled">
        <a-switch v-model:checked="form.isEnabled" />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import type {
  RateLimitAlgorithmValue,
  RateLimitPolicyFormData,
  RateLimitPolicyItem,
} from '@/api/modules/gateway'

const props = defineProps<{
  open: boolean
  saving: boolean
  editing: RateLimitPolicyItem | null
  routeOptions: Array<{ value: string, label: string }>
  globalPolicyExists: boolean
}>()

const emit = defineEmits<{
  'update:open': [open: boolean]
  'submit': [data: RateLimitPolicyFormData]
}>()

const ALGORITHM_OPTIONS = [
  { value: 0, label: '固定窗口' },
  { value: 1, label: '滑动窗口' },
  { value: 2, label: '令牌桶' },
]

const formRef = ref<FormInstance>()
const form = reactive({
  name: '',
  scope: 0 as 0 | 1,
  routeId: undefined as string | undefined,
  algorithm: undefined as RateLimitAlgorithmValue | undefined,
  permitLimit: undefined as number | undefined,
  windowSeconds: undefined as number | undefined,
  segmentsPerWindow: undefined as number | undefined,
  tokenLimit: undefined as number | undefined,
  tokensPerPeriod: undefined as number | undefined,
  replenishmentPeriodSeconds: undefined as number | undefined,
  queueLimit: 0,
  isEnabled: true,
})

/** 新增且已有全局策略时禁用「全局」选项 */
const globalOptionDisabled = computed(() =>
  props.globalPolicyExists && props.editing?.scope !== 0,
)

/** 目标路由选项：编辑时若路由已删除，追加兜底选项保证可展示 */
const routeSelectOptions = computed(() => {
  const current = props.editing?.routeId
  if (current && !props.routeOptions.some((o) => o.value === current)) {
    return [...props.routeOptions, { value: current, label: `${current}（路由已删除）` }]
  }
  return props.routeOptions
})

const rules = computed<Record<string, Rule[]>>(() => ({
  name: [{ required: true, message: '请输入策略名称', trigger: 'blur' }],
  scope: [{ required: true, message: '请选择作用域', trigger: 'change' }],
  routeId: form.scope === 1 ? [{ required: true, message: '请选择目标路由', trigger: 'change' }] : [],
  algorithm: [{ required: true, message: '请选择限流算法', trigger: 'change' }],
  permitLimit: form.algorithm === 0 || form.algorithm === 1
    ? [{ required: true, message: '请输入窗口内许可数', trigger: 'blur' }]
    : [],
  windowSeconds: form.algorithm === 0 || form.algorithm === 1
    ? [{ required: true, message: '请输入窗口时长', trigger: 'blur' }]
    : [],
  segmentsPerWindow: form.algorithm === 1
    ? [{ required: true, message: '请输入每窗口分段数', trigger: 'blur' }]
    : [],
  tokenLimit: form.algorithm === 2
    ? [{ required: true, message: '请输入令牌桶容量', trigger: 'blur' }]
    : [],
  tokensPerPeriod: form.algorithm === 2
    ? [{ required: true, message: '请输入每周期补充令牌数', trigger: 'blur' }]
    : [],
  replenishmentPeriodSeconds: form.algorithm === 2
    ? [{ required: true, message: '请输入补充周期', trigger: 'blur' }]
    : [],
}))

/** 切换算法时清空其他算法的参数字段，避免残留 */
function handleAlgorithmChange(value: unknown): void {
  const algorithm = value as RateLimitAlgorithmValue
  if (algorithm === 0 || algorithm === 1) {
    form.tokenLimit = undefined
    form.tokensPerPeriod = undefined
    form.replenishmentPeriodSeconds = undefined
  }
  if (algorithm === 2 || algorithm === 0) {
    form.segmentsPerWindow = undefined
  }
  if (algorithm === 2) {
    form.permitLimit = undefined
    form.windowSeconds = undefined
  }
  formRef.value?.clearValidate()
}

watch(() => props.open, (open) => {
  if (!open) return
  const p = props.editing
  form.name = p?.name ?? ''
  form.scope = p?.scope ?? 0
  form.routeId = p?.routeId ?? undefined
  form.algorithm = p?.algorithm ?? undefined
  form.permitLimit = p?.permitLimit ?? undefined
  form.windowSeconds = p?.windowSeconds ?? undefined
  form.segmentsPerWindow = p?.segmentsPerWindow ?? undefined
  form.tokenLimit = p?.tokenLimit ?? undefined
  form.tokensPerPeriod = p?.tokensPerPeriod ?? undefined
  form.replenishmentPeriodSeconds = p?.replenishmentPeriodSeconds ?? undefined
  form.queueLimit = p?.queueLimit ?? 0
  form.isEnabled = p?.isEnabled ?? true
  formRef.value?.clearValidate()
})

async function handleOk(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  const isWindowAlgorithm = form.algorithm === 0 || form.algorithm === 1
  emit('submit', {
    name: form.name.trim(),
    scope: form.scope,
    routeId: form.scope === 1 ? form.routeId : undefined,
    algorithm: form.algorithm!,
    permitLimit: isWindowAlgorithm ? form.permitLimit : undefined,
    windowSeconds: isWindowAlgorithm ? form.windowSeconds : undefined,
    segmentsPerWindow: form.algorithm === 1 ? form.segmentsPerWindow : undefined,
    tokenLimit: form.algorithm === 2 ? form.tokenLimit : undefined,
    tokensPerPeriod: form.algorithm === 2 ? form.tokensPerPeriod : undefined,
    replenishmentPeriodSeconds: form.algorithm === 2 ? form.replenishmentPeriodSeconds : undefined,
    queueLimit: form.queueLimit ?? 0,
    isEnabled: form.isEnabled,
  })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.policy-form__hint {
  margin-top: 4px;
  color: @text-tertiary;
  font-size: @font-size-xs;
  line-height: 1.5;
}

.policy-form__row {
  display: flex;
  gap: @spacing-base;
}

.policy-form__row-item {
  flex: 1;
  min-width: 0;
}
</style>
