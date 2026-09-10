<template>
  <div class="role-menu-tab">
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
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { PermTreeNode } from '../types'

const props = defineProps<{
  checkedKeys: string[]
  tree: PermTreeNode[]
}>()

const emit = defineEmits<{
  change: [keys: string[], halfCheckedKeys: string[]]
}>()

const localKeys = ref<string[]>([...props.checkedKeys])

watch(() => props.checkedKeys, (v) => {
  localKeys.value = [...v]
})

/** checkedKeys 只含全选节点；半选父节点（部分按钮被勾的菜单）从 info.halfCheckedKeys 一并抛出 */
function onCheck(checked: unknown, info: { halfCheckedKeys?: unknown }): void {
  const keys = (Array.isArray(checked) ? checked : (checked as { checked: string[] }).checked) as string[]
  localKeys.value = [...keys]
  emit('change', [...keys], [...((info.halfCheckedKeys ?? []) as string[])])
}
</script>
