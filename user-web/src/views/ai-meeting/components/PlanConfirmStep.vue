<template>
  <SectionCard title="信息确认" flush>
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

watch(
  () => props.plan,
  (plan) => {
    if (!plan) return
    form.date = plan.date?.slice(0, 10) || form.date
    form.weather = plan.weather ?? ''
    form.tasks = plan.tasks ?? ''
    form.riskPoints = plan.riskPoints ?? ''
  },
  { immediate: true },
)

function onSubmit(): void {
  emit('submit', { ...form })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.plan-confirm__actions {
  display: flex;
  gap: @spacing-md;

  > * {
    flex: 1;
  }
}
</style>
