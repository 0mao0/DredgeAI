import type { AdminStats, LineChartData, PieChartData, DashboardMetric, SystemLog } from '@shared/types'

export const mockAdminStats: AdminStats = {
  totalUsers: 2846,
  totalApps: 10,
  totalApiCalls: 1258300,
  activeUsers: 1243,
  userTrend: 12.5,
  appTrend: 5.2,
  apiTrend: 23.8,
  activeUserTrend: 8.1,
}

export const mockMetrics: DashboardMetric[] = [
  { id: '1', title: '总用户数', value: '2,846', suffix: '人', trend: 12.5, trendUp: true, icon: 'TeamOutlined', color: '#0EA5E9' },
  { id: '2', title: '活跃用户', value: '1,243', suffix: '人', trend: 8.1, trendUp: true, icon: 'UserSwitchOutlined', color: '#10B981' },
  { id: '3', title: 'API 调用', value: '125.8', suffix: '万次', trend: 23.8, trendUp: true, icon: 'ApiOutlined', color: '#F59E0B' },
  { id: '4', title: '应用数', value: '10', suffix: '个', trend: 5.2, trendUp: true, icon: 'AppstoreOutlined', color: '#8B5CF6' },
]

export const mockApiCallsTrend: LineChartData = {
  categories: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'],
  series: [
    { name: '调用次数', data: [62, 78, 95, 88, 112, 128, 145, 138, 156, 172, 188, 210] },
  ],
}

export const mockAppDistribution: PieChartData = {
  name: '应用分布',
  data: [
    { name: '标准查询', value: 28 },
    { name: 'AI视频', value: 14 },
    { name: 'AI配音', value: 12 },
    { name: '施工经验', value: 11 },
    { name: '施组审核', value: 6 },
    { name: '耙吸效率', value: 5 },
    { name: '投标审核', value: 7 },
    { name: '情报采集', value: 4 },
    { name: '设计经验', value: 7 },
  ],
}

export const mockActiveUsersTrend: LineChartData = {
  categories: ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'],
  series: [
    { name: '日活跃', data: [320, 380, 450, 420, 510, 580, 620, 590, 650, 720, 780, 850] },
    { name: '月活跃', data: [680, 750, 820, 790, 880, 950, 1020, 980, 1050, 1120, 1180, 1243] },
  ],
}

export const mockRecentLogs: SystemLog[] = [
  { id: '1', type: '操作日志', operator: '管理员', content: '修改了「标准查询」应用配置', createdAt: '2026-07-18 09:30:00', level: 'info' },
  { id: '2', type: '安全告警', operator: '系统', content: '检测到异常 API 调用频率', createdAt: '2026-07-18 08:15:00', level: 'warning' },
  { id: '3', type: '系统错误', operator: '系统', content: '数据处理任务 #1023 执行失败', createdAt: '2026-07-18 07:45:00', level: 'error' },
  { id: '4', type: '操作日志', operator: '张三', content: '新增角色「数据分析师」', createdAt: '2026-07-17 16:20:00', level: 'info' },
  { id: '5', type: '登录日志', operator: '李四', content: '登录系统（IP: 192.168.1.100）', createdAt: '2026-07-17 15:00:00', level: 'info' },
]
