<template>
  <SectionCard title="会前录入" flush>
    <a-form layout="vertical">
      <a-form-item label="日期">
        <a-date-picker v-model:value="form.date" value-format="YYYY-MM-DD" style="width: 100%" />
      </a-form-item>
      <a-form-item label="天气">
        <a-input v-model:value="form.weather" placeholder="如：晴，28℃" />
      </a-form-item>
      <a-form-item label="今日任务">
        <a-textarea v-model:value="form.tasks" :rows="3" placeholder="今日施工任务" />
      </a-form-item>
      <a-form-item label="风险点">
        <a-textarea v-model:value="form.riskPoints" :rows="3" placeholder="安全风险提示" />
      </a-form-item>
      <AppButton variant="primary" size="lg" block :loading="loading" @click="onSubmit">
        保存并生成晨会稿
      </AppButton>
    </a-form>
  </SectionCard>
</template>

<script setup lang="ts">
import { reactive } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { PreInfo } from '@/types'

defineProps<{ loading: boolean }>()
const emit = defineEmits<{ submit: [preInfo: PreInfo] }>()

const form = reactive<PreInfo>({
  date: new Date().toISOString().slice(0, 10),
  weather: '',
  tasks: '',
  riskPoints: '',
})

function onSubmit(): void {
  emit('submit', { ...form })
}
</script>
