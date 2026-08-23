import type { ApplicationItem } from '@shared/types'

/** 10 个应用模块，与 user-web 共享同一套数据。分类：通用 | 经营 | 设计 | 施工 */
export const mockApplications: ApplicationItem[] = [
  { id: '1', name: '规范问答', category: '通用', manager: '李文', version: 'v2.1.0', status: '运营中', userCount: 1240, apiCalls: 256000, createdAt: '2026-01-15', icon: 'BookOutlined', route: '/applications/standard' },
  { id: '2', name: 'AI视频', category: '通用', manager: '周晓', version: 'v1.3.0', status: '运营中', userCount: 620, apiCalls: 98000, createdAt: '2026-02-20', icon: 'VideoCameraOutlined', route: '/applications/ai-video' },
  { id: '3', name: 'AI 配音', category: '通用', manager: '陈晨', version: 'v1.5.2', status: '运营中', userCount: 540, apiCalls: 72000, createdAt: '2026-03-10', icon: 'CustomerServiceOutlined', route: '/applications/dubbing' },
  { id: '4', name: '设计经验', category: '设计', manager: '吴敏', version: 'v1.0.0', status: '运营中', userCount: 320, apiCalls: 41000, createdAt: '2026-04-01', icon: 'BulbOutlined', route: '/applications/design-experience' },
  { id: '5', name: '施工经验', category: '施工', manager: '赵磊', version: 'v1.1.0', status: '运营中', userCount: 480, apiCalls: 63000, createdAt: '2026-03-15', icon: 'ToolOutlined', route: '/applications/construction-experience' },
  { id: '6', name: '施组审核', category: '施工', manager: '孙浩', version: 'v2.0.1', status: '运营中', userCount: 260, apiCalls: 58000, createdAt: '2026-02-08', icon: 'FileProtectOutlined', route: '/applications/construction-plan-review' },
  { id: '7', name: '耙吸效率', category: '施工', manager: '郑涛', version: 'v1.4.0', status: '运营中', userCount: 210, apiCalls: 89000, createdAt: '2026-05-01', icon: 'DashboardOutlined', route: '/applications/trailing-suction-efficiency' },
  { id: '8', name: '情报采集', category: '经营', manager: '王琳', version: 'v1.2.0', status: '运营中', userCount: 180, apiCalls: 34000, createdAt: '2026-04-15', icon: 'RadarChartOutlined', route: '/applications/intelligence', subApps: [
    { id: '8-1', name: '疏浚情报', category: '经营', parentAppId: '8', parentAppName: '情报采集', route: '/intelligence/dredge', icon: 'RadarChartOutlined', version: 'v1.0.0', status: '已发布', scope: '所有', description: '聚焦疏浚行业的科技与工程情报，由后台采集并结构化后发布' },
    { id: '8-2', name: '科技情报', category: '经营', parentAppId: '8', parentAppName: '情报采集', route: '/intelligence/tech', icon: 'ExperimentOutlined', version: 'v1.0.0', status: '已发布', scope: '所有', description: '通用科技前沿情报，支持用户订阅与智能推送' },
  ] },
  { id: '9', name: 'AI投标', category: '经营', manager: '冯杰', version: 'v3.0.2', status: '运营中', userCount: 290, apiCalls: 76000, createdAt: '2026-01-01', icon: 'FileSearchOutlined', route: '/applications/ai-bid' },
  { id: '10', name: 'AI晨会', category: '施工', manager: '刘洋', version: 'v0.1.0', status: '运营中', userCount: 0, apiCalls: 0, createdAt: '2026-08-23', icon: 'TeamOutlined', route: '/applications/ai-meeting', scope: '所有' },
]
