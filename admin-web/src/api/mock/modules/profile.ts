import type MockAdapter from 'axios-mock-adapter'
import type { UserInfo } from '@/types'

const mockProfile: UserInfo = {
  id: '1',
  username: 'admin',
  name: '管理员',
  email: 'admin@dredgeai.com',
  phone: '138-0000-0000',
  role: 'super_admin',
  department: '技术部',
  avatar: '',
  status: '启用',
  createdAt: '2025-01-01',
  lastLogin: '2026-07-18 09:30:00',
}

export function registerProfileMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/profile').reply(wrap(() => mockProfile))
}
