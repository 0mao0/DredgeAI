<template>
  <div class="page-skeleton">
    <a-skeleton :title="{ width: '30%' }" :paragraph="{ rows: 1, width: '50%' }" active />
    <div class="skeleton-grid">
      <a-card v-for="i in cardCount" :key="i">
        <a-skeleton :paragraph="{ rows: 2 }" active />
      </a-card>
    </div>
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ cardCount?: number }>(), { cardCount: 4 })
</script>

<style scoped lang="less">
.page-skeleton {
  padding: 24px;
}
.skeleton-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 16px;
  margin-top: 24px;
}
</style>
