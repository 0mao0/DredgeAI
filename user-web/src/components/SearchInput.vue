<template>
  <a-input
    :value="modelValue"
    :placeholder="placeholder"
    allow-clear
    class="search-input"
    @update:value="$emit('update:modelValue', $event)"
    @press-enter="$emit('search', $event)"
  >
    <template #prefix>
      <SearchOutlined class="search-icon" />
    </template>
  </a-input>
</template>

<script setup lang="ts">
import { SearchOutlined } from '@ant-design/icons-vue'

defineProps<{ modelValue: string; placeholder?: string }>()
defineEmits<{ 'update:modelValue': [string]; search: [unknown] }>()
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.search-input {
  border-radius: @radius-base;
}
.search-icon {
  color: @text-tertiary;
}
</style>
