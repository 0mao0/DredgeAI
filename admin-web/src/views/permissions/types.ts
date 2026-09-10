import type { VNode } from 'vue'

export interface PermTreeNode {
  title: string | VNode
  key: string
  children?: PermTreeNode[]
  selectable?: boolean
  /** a-tree 节点属性：禁用复选框（无权限码的菜单/分组节点只展示不可勾选） */
  disableCheckbox?: boolean
}
