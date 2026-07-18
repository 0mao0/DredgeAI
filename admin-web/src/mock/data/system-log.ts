import type { SystemLog } from '@/types'

export const mockSystemLogs: SystemLog[] = [
  { id: '1', type: '操作日志', operator: '管理员', content: '修改了「智能审批」应用配置', ip: '192.168.1.1', createdAt: '2026-07-18 09:30:00', level: 'info' },
  { id: '2', type: '登录日志', operator: '张三', content: '登录系统', ip: '192.168.1.100', createdAt: '2026-07-18 09:15:00', level: 'info' },
  { id: '3', type: '安全告警', operator: '系统', content: '检测到异常 API 调用频率', ip: '10.0.0.5', createdAt: '2026-07-18 08:15:00', level: 'warning' },
  { id: '4', type: '系统错误', operator: '系统', content: '数据处理任务 #1023 执行失败：数据库连接超时', createdAt: '2026-07-18 07:45:00', level: 'error' },
  { id: '5', type: '操作日志', operator: '李四', content: '新增角色「数据分析师」', ip: '192.168.1.101', createdAt: '2026-07-17 16:20:00', level: 'info' },
  { id: '6', type: '操作日志', operator: '王五', content: '发布了应用「合规检查工具」v0.9.0', ip: '192.168.1.102', createdAt: '2026-07-17 14:30:00', level: 'info' },
  { id: '7', type: '安全告警', operator: '系统', content: '用户「test_user」连续登录失败 5 次', ip: '203.0.113.1', createdAt: '2026-07-17 10:00:00', level: 'warning' },
  { id: '8', type: '登录日志', operator: '赵六', content: '登录系统', ip: '192.168.1.103', createdAt: '2026-07-17 09:45:00', level: 'info' },
  { id: '9', type: '系统错误', operator: '系统', content: '知识检索服务 #2 节点宕机', createdAt: '2026-07-17 03:20:00', level: 'error' },
  { id: '10', type: '操作日志', operator: '管理员', content: '更新了用户「张三」的角色权限', ip: '192.168.1.1', createdAt: '2026-07-16 17:00:00', level: 'info' },
]
