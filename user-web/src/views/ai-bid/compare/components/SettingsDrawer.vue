<template>
  <a-drawer
    :open="open"
    title="基础要求设置"
    placement="right"
    width="360"
    @close="emit('update:open', false)"
  >
    <div class="settings">
      <div class="settings__group">
        <div class="settings__label">分析项</div>
        <div class="settings__row">
          <span>元数据比对</span>
          <a-switch v-model:checked="form.metadata" size="small" />
        </div>
        <div class="settings__row">
          <span>报价规律分析</span>
          <a-switch v-model:checked="form.price" size="small" />
        </div>
        <div class="settings__row">
          <span>条款响应核对</span>
          <a-switch v-model:checked="form.clause" size="small" />
        </div>
        <div class="settings__row">
          <span>AI 综合研判</span>
          <a-switch v-model:checked="form.ai" size="small" />
        </div>
      </div>

      <div class="settings__group">
        <div class="settings__label">雷同判定阈值（{{ form.threshold }}%）</div>
        <a-slider v-model:value="form.threshold" :min="50" :max="95" :step="5" />
        <div class="settings__hint">两文档相似度超过该值即标记为雷同证据</div>
      </div>
    </div>

    <template #footer>
      <div class="settings__footer">
        <a-button @click="reset">恢复默认</a-button>
        <a-button type="primary" @click="handleSave">保存设置</a-button>
      </div>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { reactive } from 'vue'

export interface CompareSettings {
  metadata: boolean
  price: boolean
  clause: boolean
  ai: boolean
  threshold: number
}

defineProps<{ open: boolean }>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'save': [settings: CompareSettings]
}>()

const DEFAULTS: CompareSettings = { metadata: true, price: true, clause: true, ai: true, threshold: 75 }
const form = reactive<CompareSettings>({ ...DEFAULTS })

function reset(): void {
  Object.assign(form, DEFAULTS)
}

function handleSave(): void {
  emit('save', { ...form })
  emit('update:open', false)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.settings {
  display: flex;
  flex-direction: column;
  gap: @spacing-xl;

  &__group {
    display: flex;
    flex-direction: column;
    gap: @spacing-md;
  }

  &__label {
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
  }

  &__row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    font-size: @font-size-sm;
    color: @text-secondary;
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__footer {
    display: flex;
    justify-content: flex-end;
    gap: @spacing-sm;
  }
}
</style>
