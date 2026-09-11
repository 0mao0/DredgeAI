import type { ApplicationItem } from '@shared/types'

/** 10 个应用模块，与 user-web 共享同一套数据。分类/状态为后端枚举 wire 值（snake_case） */
export const mockApplications: ApplicationItem[] = [
  { id: 'ff1e69b2-ddfd-4406-b8f9-024db34081cd', name: '规范问答', category: 'general', manager: '李文', version: 'v2.1.0', status: 'online', userCount: 1240, apiCalls: 256000, createdAt: '2026-01-15', icon: 'BookOutlined', route: '/applications/standard' },
  { id: '70b2ce01-f400-4947-bcf4-0d5f01568783', name: 'AI视频', category: 'general', manager: '周晓', version: 'v1.3.0', status: 'online', userCount: 620, apiCalls: 98000, createdAt: '2026-02-20', icon: 'VideoCameraOutlined', route: '/applications/ai-video' },
  { id: '9076f8d6-53a3-490e-b28b-43f69eadf11e', name: 'AI 配音', category: 'general', manager: '陈晨', version: 'v1.5.2', status: 'online', userCount: 540, apiCalls: 72000, createdAt: '2026-03-10', icon: 'CustomerServiceOutlined', route: '/applications/dubbing' },
  { id: '293781c9-841b-428f-ab9d-f3c3117ce138', name: '设计经验', category: 'design', manager: '吴敏', version: 'v1.0.0', status: 'online', userCount: 320, apiCalls: 41000, createdAt: '2026-04-01', icon: 'BulbOutlined', route: '/applications/design-experience' },
  { id: 'fc005209-8641-4fd9-95f9-9acb632912b6', name: '施工经验', category: 'construction', manager: '赵磊', version: 'v1.1.0', status: 'online', userCount: 480, apiCalls: 63000, createdAt: '2026-03-15', icon: 'ToolOutlined', route: '/applications/construction-experience' },
  { id: '0283801c-7a32-4dc0-96d3-eaee1b1f4ee2', name: '施组审核', category: 'construction', manager: '孙浩', version: 'v2.0.1', status: 'online', userCount: 260, apiCalls: 58000, createdAt: '2026-02-08', icon: 'FileProtectOutlined', route: '/applications/construction-plan-review' },
  { id: '6dd9583d-169d-43d1-acd0-c1432248b898', name: '耙吸效率', category: 'construction', manager: '郑涛', version: 'v1.4.0', status: 'online', userCount: 210, apiCalls: 89000, createdAt: '2026-05-01', icon: 'DashboardOutlined', route: '/applications/trailing-suction-efficiency' },
  { id: '951ee79b-247e-4473-88a1-19e91c6d40ed', name: '情报采集', category: 'operation', manager: '王琳', version: 'v1.2.0', status: 'online', userCount: 180, apiCalls: 34000, createdAt: '2026-04-15', icon: 'RadarChartOutlined', route: '/applications/intelligence', subApps: [
    { id: '5c264893-1107-4de1-800a-52c178b9923f', name: '疏浚情报', category: 'operation', parentAppId: '951ee79b-247e-4473-88a1-19e91c6d40ed', route: '/intelligence/dredge', icon: 'RadarChartOutlined', version: 'v1.0.0', status: 'published', description: '聚焦疏浚行业的科技与工程情报，由后台采集并结构化后发布' },
    { id: 'a7a9b0d2-ebcf-48da-9931-b236ef721c44', name: '科技情报', category: 'operation', parentAppId: '951ee79b-247e-4473-88a1-19e91c6d40ed', route: '/intelligence/tech', icon: 'ExperimentOutlined', version: 'v1.0.0', status: 'published', description: '通用科技前沿情报，支持用户订阅与智能推送' },
  ] },
  { id: '8da83abb-fdc5-480a-87cc-dca712243ffd', name: 'AI投标', category: 'operation', manager: '冯杰', version: 'v3.0.2', status: 'online', userCount: 290, apiCalls: 76000, createdAt: '2026-01-01', icon: 'FileSearchOutlined', route: '/applications/ai-bid' },
  { id: '50e3cb9e-76a1-40ca-8034-060e7255bf5f', name: 'AI晨会', category: 'construction', manager: '刘洋', version: 'v0.1.0', status: 'online', userCount: 0, apiCalls: 0, createdAt: '2026-08-23', icon: 'TeamOutlined', route: '/applications/ai-meeting' },
]
