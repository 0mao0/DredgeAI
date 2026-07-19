export interface TaskItem {
  id: string
  title: string
  status: '进行中' | '已完成' | '已暂停' | '已失败'
  updatedAt: string
  app?: string
  progress?: number
}

export interface QuickTask {
  id: string
  title: string
  tag: string
  route: string
  icon: string
}
