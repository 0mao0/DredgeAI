<template>
  <a-modal
    :open="open"
    title="导出报告"
    width="440px"
    :confirm-loading="exporting"
    ok-text="开始导出"
    cancel-text="取消"
    destroy-on-close
    @ok="handleOk"
    @cancel="emit('update:open', false)"
  >
    <a-radio-group v-model:value="format" class="export-options">
      <a-radio value="docx">Word 报告（含原文截图与高亮框）</a-radio>
      <a-radio value="pdf">PDF 摘要（结论与关键图表）</a-radio>
    </a-radio-group>
  </a-modal>
</template>

<script setup lang="ts">
import { ref } from 'vue'

defineProps<{
  open: boolean
  exporting?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'confirm': [format: 'docx' | 'pdf']
}>()

const format = ref<'docx' | 'pdf'>('docx')

function handleOk(): void {
  emit('confirm', format.value)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.export-options {
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  font-size: @font-size-sm;
}
</style>
