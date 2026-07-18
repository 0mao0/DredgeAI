import type MockAdapter from 'axios-mock-adapter'
import { mockApplications } from '@/mock/applications'

export function registerApplicationMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/applications').reply(wrap(() => mockApplications))
}
