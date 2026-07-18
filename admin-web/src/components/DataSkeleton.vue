<template>
  <div class="data-skeleton">
    <a-skeleton v-for="i in rows" :key="i" :title="{ width: '60%' }" :paragraph="{ rows: 2 }" active />
  </div>
</template>

<script setup lang="ts">
withDefaults(defineProps<{ rows?: number }>(), { rows: 3 })
</script>

<style scoped lang="less">
.data-skeleton > * + * {
  margin-top: 16px;
}
</style>
