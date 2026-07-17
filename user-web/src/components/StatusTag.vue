<template>
  <a-tag :color="colorMap[status] || 'default'">{{ status }}</a-tag>
</template>

<script setup lang="ts">
const colorMap: Record<string, string> = {
  '已授权': 'green',
  '已上架': 'green',
  '启用': 'green',
  '已完成': 'green',
  '已通过': 'green',
  '进行中': 'blue',
  '待审核': 'orange',
  '待申请': 'orange',
  '已暂停': 'orange',
  '已下架': 'red',
  '已禁用': 'red',
  '已拒绝': 'red',
  '已失败': 'red',
  '高风险': 'red',
  '中风险': 'orange',
  '低风险': 'blue',
}

defineProps<{ status: string }>()
</script>
