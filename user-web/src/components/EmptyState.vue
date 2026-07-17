<template>
  <div class="empty-state">
    <a-empty :description="description" />
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ description?: string }>(), { description: '暂无数据' })
</script>

<style scoped lang="less">
.empty-state {
  padding: 48px 0;
}
</style>
