import type { Role } from '@shared/types'

export const mockRoles: Role[] = [
  {
    id: '1',
    name: '超级管理员',
    description: '拥有所有模块的全部权限',
    menuKeys: ['*'],
    appIds: ['*'],
    userCount: 3,
    createdAt: '2025-01-01',
  },
  {
    id: '2',
    name: '管理员',
    description: '管理日常运营，可查看和管理大部分模块',
    menuKeys: ['/dashboard', '/org-users', '/permissions', '/alerts', '/knowledge', '/applications'],
    appIds: ['8-1', '1'],
    userCount: 6,
    createdAt: '2025-01-01',
  },
  {
    id: '3',
    name: '运营人员',
    description: '负责应用运营和数据分析',
    menuKeys: ['/dashboard', '/applications', '/knowledge'],
    appIds: ['2', '4'],
    userCount: 8,
    createdAt: '2025-01-15',
  },
  {
    id: '4',
    name: '工程师',
    description: '技术工程师，使用知识库和应用工具',
    menuKeys: ['/dashboard', '/knowledge'],
    appIds: ['6', '8-1'],
    userCount: 7,
    createdAt: '2025-02-01',
  },
  {
    id: '5',
    name: '访客',
    description: '只读权限，可浏览仪表盘和知识库',
    menuKeys: ['/dashboard', '/knowledge'],
    appIds: ['4'],
    userCount: 4,
    createdAt: '2025-03-01',
  },
]
