import type { VNode } from 'vue'

export interface PermTreeNode {
  title: string | VNode
  key: string
  children?: PermTreeNode[]
  selectable?: boolean
}
