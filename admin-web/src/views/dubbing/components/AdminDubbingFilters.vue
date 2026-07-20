<template>
  <div class="filter-bar">
    <a-input-search
      v-model:value="keyword"
      placeholder="搜索用户/文本"
      class="filter-search"
      allow-clear
      @search="emitChange"
      @change="onKeywordChange"
    />
    <a-select v-model:value="status" class="filter-select">
      <a-select-option value="">全部</a-select-option>
      <a-select-option value="生成中">生成中</a-select-option>
      <a-select-option value="已完成">已完成</a-select-option>
      <a-select-option value="已失败">已失败</a-select-option>
    </a-select>
    <div class="filter-spacer" />
    <div class="filter-item">
      <span class="filter-label">仅看用户已删除</span>
      <a-switch v-model:checked="deletedOnly" @change="emitChange" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

defineProps<{ loading: boolean }>()
const emit = defineEmits<{
  search: [filters: { keyword: string; status: string; deletedOnly: boolean; dateRange: [string, string] }]
  reset: []
}>()

const keyword = ref('')
const status = ref('')
const deletedOnly = ref(false)

function buildFilters() {
  return {
    keyword: keyword.value,
    status: status.value,
    deletedOnly: deletedOnly.value,
    dateRange: ['', ''] as [string, string],
  }
}

function emitChange(): void {
  emit('search', buildFilters())
}

function onKeywordChange(): void {
  emit('search', buildFilters())
}
</script>

<style scoped lang="less">
.filter-bar {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  align-items: center;
  margin-bottom: 16px;
}
.filter-search {
  width: 240px;
  max-width: 100%;
}
.filter-select {
  width: 140px;
  max-width: 100%;
}
.filter-spacer {
  flex: 1 1 auto;
}
.filter-item {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}
.filter-label {
  font-size: 13px;
  white-space: nowrap;
}
@media (max-width: 576px) {
  .filter-search, .filter-select { width: 100%; }
  .filter-spacer { display: none; }
  .filter-item { margin-left: 0; }
}
</style>
