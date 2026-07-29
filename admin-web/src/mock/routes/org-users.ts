import type MockAdapter from 'axios-mock-adapter'
import { mockOrgUsers } from '@shared/mock/data/org-users'

export function registerOrgUsersMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/org-users').reply(
    wrap(() => ({
      items: [...mockOrgUsers],
      total: mockOrgUsers.length,
    })),
  )

  mock.onPut(/\/api\/admin\/org-users\/[^/]+\/status$/).reply(
    wrap(() => null),
  )

  mock.onPut(/\/api\/admin\/org-users\/[^/]+\/roles$/).reply(
    wrap(() => null),
  )
}
