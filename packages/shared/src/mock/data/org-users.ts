import type { OrgUser } from '@shared/types'

export const mockOrgUsers: OrgUser[] = [
  { id: '1', name: '张建国', phone: '13800138001', departments: ['工程技术部'], status: 'active', roleIds: ['1', '4'], createdAt: '2025-03-01' },
  { id: '2', name: '李明', phone: '13800138002', departments: ['安全质量部'], status: 'active', roleIds: ['2', '4'], createdAt: '2025-03-05' },
  { id: '3', name: '王小红', phone: '13800138003', departments: ['经营开发部', '市场部'], status: 'active', roleIds: ['3'], createdAt: '2025-04-10' },
  { id: '4', name: '陈伟', phone: '13800138004', departments: ['施工管理部'], status: 'active', roleIds: ['2', '3'], createdAt: '2025-04-15' },
  { id: '5', name: '赵敏', phone: '13800138005', departments: ['工程技术部'], status: 'disabled', roleIds: ['4'], createdAt: '2025-05-01' },
  { id: '6', name: '刘洋', phone: '13800138006', departments: ['经营开发部'], status: 'active', roleIds: ['3', '5'], createdAt: '2025-05-10' },
  { id: '7', name: '孙丽华', phone: '13800138007', departments: ['工程技术部', '设计部'], status: 'active', roleIds: ['1', '4'], createdAt: '2025-06-01' },
  { id: '8', name: '周强', phone: '13800138008', departments: ['安全质量部'], status: 'active', roleIds: ['2'], createdAt: '2025-06-15' },
  { id: '9', name: '吴芳', phone: '13800138009', departments: ['市场部'], status: 'active', roleIds: ['3', '5'], createdAt: '2025-07-01' },
  { id: '10', name: '郑杰', phone: '13800138010', departments: ['施工管理部'], status: 'active', roleIds: ['3'], createdAt: '2025-07-10' },
  { id: '11', name: '冯涛', phone: '13800138011', departments: ['设计部'], status: 'active', roleIds: ['1', '3'], createdAt: '2025-08-01' },
  { id: '12', name: '陈晓明', phone: '13800138012', departments: ['工程技术部'], status: 'active', roleIds: ['4'], createdAt: '2025-08-15' },
  { id: '13', name: '林志远', phone: '13800138013', departments: ['施工管理部', '工程技术部'], status: 'active', roleIds: ['2', '3', '4'], createdAt: '2025-09-01' },
  { id: '14', name: '黄丽', phone: '13800138014', departments: ['经营开发部'], status: 'active', roleIds: ['3'], createdAt: '2025-09-10' },
  { id: '15', name: '何伟强', phone: '13800138015', departments: ['安全质量部'], status: 'disabled', roleIds: ['4'], createdAt: '2025-10-01' },
  { id: '16', name: '马超', phone: '13800138016', departments: ['市场部'], status: 'active', roleIds: ['5'], createdAt: '2025-10-15' },
  { id: '17', name: '罗文', phone: '13800138017', departments: ['设计部', '工程技术部'], status: 'active', roleIds: ['4', '5'], createdAt: '2025-11-01' },
  { id: '18', name: '谢芳华', phone: '13800138018', departments: ['施工管理部'], status: 'active', roleIds: ['2'], createdAt: '2025-11-10' },
  { id: '19', name: '邓国栋', phone: '13800138019', departments: ['经营开发部'], status: 'active', roleIds: ['3'], createdAt: '2025-12-01' },
  { id: '20', name: '彭丽娟', phone: '13800138020', departments: ['安全质量部', '施工管理部'], status: 'active', roleIds: ['2', '3'], createdAt: '2025-12-10' },
]
