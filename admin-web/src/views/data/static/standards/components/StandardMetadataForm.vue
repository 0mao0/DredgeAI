<template>
  <a-form layout="horizontal" :label-col="{ flex: '72px' }" :wrapper-col="{ flex: '1' }" class="standard-metadata-form">
    <a-row :gutter="12">
      <a-col :span="12">
        <a-form-item label="名称" required>
          <a-input v-model:value="form.name" :disabled="disabled" placeholder="请输入标准名称" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="编号" required>
          <a-input v-model:value="form.code" :disabled="disabled" placeholder="请输入标准编号" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="行业">
          <a-select v-model:value="form.industry" :options="industrySelectOptions" :disabled="disabled" allow-clear placeholder="请选择行业" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="性质">
          <a-select v-model:value="form.nature" :options="natureSelectOptions" :disabled="disabled" allow-clear placeholder="请选择性质" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="级别">
          <a-select v-model:value="form.level" :options="levelSelectOptions" :disabled="disabled" allow-clear placeholder="请选择级别" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="状态">
          <a-select v-model:value="form.status" :options="statusSelectOptions" :disabled="disabled" allow-clear placeholder="请选择状态" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="发布部门">
          <a-input v-model:value="form.issuer" :disabled="disabled" placeholder="请输入发布部门" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="发布年份">
          <a-select v-model:value="form.publishYear" :options="yearSelectOptions" :disabled="disabled" allow-clear placeholder="请选择发布年份" />
        </a-form-item>
      </a-col>
      <a-col :span="12">
        <a-form-item label="上传人">
          <a-input v-model:value="form.uploader" :disabled="disabled" placeholder="请输入上传人" />
        </a-form-item>
      </a-col>
    </a-row>
    <a-form-item label="简介">
      <a-textarea v-model:value="form.description" :rows="2" :disabled="disabled" placeholder="请输入标准简介" />
    </a-form-item>
  </a-form>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { StandardPropertyInput } from '@/types'
import {
  industrySelectOptions,
  levelSelectOptions,
  natureSelectOptions,
  statusSelectOptions,
  yearSelectOptions,
} from '../constants'

const props = defineProps<{
  modelValue: StandardPropertyInput
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: StandardPropertyInput]
}>()

const form = reactive<StandardPropertyInput>({ ...props.modelValue })
let syncing = false

watch(
  () => props.modelValue,
  (value) => {
    syncing = true
    Object.assign(form, { ...value, name: value.name ?? '', code: value.code ?? '' })
    syncing = false
  },
  { immediate: true },
)

watch(
  form,
  () => {
    if (!syncing) emit('update:modelValue', { ...form })
  },
  { deep: true },
)
</script>

<style scoped lang="less">
.standard-metadata-form {
  padding-top: 4px;

  :deep(.ant-form-item) {
    margin-bottom: 8px;
  }

  :deep(.ant-form-item-label) {
    white-space: nowrap;
  }
}
</style>
