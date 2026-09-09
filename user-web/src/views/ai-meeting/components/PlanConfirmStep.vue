<template>
  <SectionCard title="信息确认" flush>
    <div v-if="parsing" class="plan-confirm__hint">
      AI 正在整理你的口述内容，结果会自动填入下方（你也可以先手动修改）
    </div>
    <a-form layout="horizontal" label-align="left" class="plan-confirm__form">
      <a-form-item
        label="日期"
        :label-col="{ flex: '0 0 80px' }"
        :wrapper-col="{ flex: 'auto' }"
      >
        <a-date-picker v-model:value="form.date" value-format="YYYY-MM-DD" style="width: 100%" />
      </a-form-item>
      <a-form-item
        label="天气"
        :label-col="{ flex: '0 0 80px' }"
        :wrapper-col="{ flex: 'auto' }"
      >
        <a-input v-model:value="form.weather" placeholder="天气未能自动获取，可手动填写" />
      </a-form-item>
      <a-form-item
        label="今日任务"
        :label-col="{ flex: '0 0 80px' }"
        :wrapper-col="{ flex: 'auto' }"
      >
        <a-textarea v-model:value="form.tasks" :rows="5" />
      </a-form-item>
      <a-form-item
        label="风险点"
        :label-col="{ flex: '0 0 80px' }"
        :wrapper-col="{ flex: 'auto' }"
      >
        <a-textarea v-model:value="form.riskPoints" :rows="3" placeholder="自动解析，可修改" />
      </a-form-item>
    </a-form>
    <div class="plan-confirm__actions">
      <AppButton size="lg" @click="emit('back')">返回修改</AppButton>
      <AppButton variant="primary" size="lg" :loading="loading" @click="onSubmit">
        生成晨会稿
      </AppButton>
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { PlanParseResult, PreInfo } from '@/types'

const props = defineProps<{
  plan: PlanParseResult | null
  loading: boolean
  /** AI 正在整理：本页已用原始输入预填，结果回来后回填未改动字段 */
  parsing?: boolean
}>()
const emit = defineEmits<{
  submit: [preInfo: PreInfo]
  back: []
}>()

const form = reactive<PreInfo>({
  date: new Date().toISOString().slice(0, 10),
  weather: '',
  tasks: '',
  riskPoints: '',
})

/** 上一次由外部写入的字段值：与当前值一致说明用户没动过，可以安全覆盖 */
const applied = { date: '', weather: '', tasks: '', riskPoints: '' }

watch(
  () => props.plan,
  (plan) => {
    if (!plan) return
    const next = {
      date: plan.date?.slice(0, 10) || form.date,
      weather: plan.weather ?? '',
      tasks: plan.tasks ?? '',
      riskPoints: plan.riskPoints ?? '',
    }
    if (!applied.tasks || form.tasks === applied.tasks) form.tasks = next.tasks
    if (!applied.riskPoints || form.riskPoints === applied.riskPoints) form.riskPoints = next.riskPoints
    if (!applied.weather || form.weather === applied.weather) form.weather = next.weather
    if (!applied.date || form.date === applied.date) form.date = next.date
    applied.date = form.date
    applied.weather = form.weather
    applied.tasks = form.tasks
    applied.riskPoints = form.riskPoints
  },
  { immediate: true },
)

function onSubmit(): void {
  emit('submit', { ...form })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.plan-confirm__hint {
  margin-bottom: @spacing-base;
  padding: @spacing-sm @spacing-base;
  border-radius: @radius-sm;
  background: color-mix(in srgb, var(--color-brand) 8%, transparent);
  color: @text-secondary;
  font-size: @font-size-sm;
}
.plan-confirm__actions {
  display: flex;
  gap: @spacing-md;

  > * {
    flex: 1;
  }
}
</style>
