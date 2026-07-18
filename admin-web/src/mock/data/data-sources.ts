import type { DataSource } from '@/types'

export const mockDataSources: DataSource[] = [
  { id: '1', name: '生产数据库', type: 'mysql', status: '已连接', lastSync: '2026-07-18 09:30:00', description: '主业务数据库' },
  { id: '2', name: '用户行为数据', type: 'postgresql', status: '已连接', lastSync: '2026-07-18 09:25:00', description: '用户埋点事件数据' },
  { id: '3', name: '第三方 API', type: 'api', status: '连接失败', lastSync: '2026-07-17 15:00:00', description: '外部数据接口' },
  { id: '4', name: '归档数据库', type: 'mysql', status: '未配置', description: '历史数据归档' },
  { id: '5', name: '文档索引库', type: 'postgresql', status: '已连接', lastSync: '2026-07-18 08:00:00', description: '全文搜索索引' },
]
