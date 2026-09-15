<template>
  <div class="role-app-tab">
    <a-spin :spinning="loading">
      <a-tree
        :checked-keys="localKeys"
        checkable
        :tree-data="tree"
        :replace-fields="{ key: 'key', title: 'title', children: 'children' }"
        selectable
        :check-strictly="false"
        default-expand-all
        @check="onCheck"
      />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { PermTreeNode } from '../types'

const props = defineProps<{
  checkedKeys: string[]
  tree: PermTreeNode[]
  loading: boolean
}>()

const emit = defineEmits<{
  change: [keys: string[]]
}>()

const localKeys = ref<string[]>([...props.checkedKeys])

watch(() => props.checkedKeys, (v) => {
  localKeys.value = [...v]
})

/** 只在用户勾选动作（@check）时抛出；watch localKeys 抛出会与父级回写 props 形成回声循环 */
function onCheck(checked: unknown): void {
  const keys = (Array.isArray(checked) ? checked : (checked as { checked: string[] }).checked) as string[]
  localKeys.value = [...keys]
  emit('change', [...keys])
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';
</style>
