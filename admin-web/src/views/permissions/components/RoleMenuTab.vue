<template>
  <div class="role-menu-tab">
    <a-tree
      v-model:checked-keys="localKeys"
      checkable
      :tree-data="tree"
      :replace-fields="{ key: 'key', title: 'title', children: 'children' }"
      selectable
      :check-strictly="false"
      default-expand-all
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
  change: [keys: string[]]
}>()

const localKeys = ref<string[]>([...props.checkedKeys])

watch(() => props.checkedKeys, (v) => {
  localKeys.value = [...v]
})

watch(localKeys, (v) => {
  emit('change', [...v])
})
</script>
