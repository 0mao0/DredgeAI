import type MockAdapter from 'axios-mock-adapter'
import { apiKeys, modelTypes, usageByModel, usageByKey } from '@/mock/apikey'

export function registerApiKeyMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/apikey/list').reply(wrap(() => apiKeys))
  mock.onGet('/apikey/models').reply(wrap(() => modelTypes))
  mock.onGet('/apikey/usage-by-model').reply(wrap(() => usageByModel))
  mock.onGet('/apikey/usage-by-key').reply(wrap(() => usageByKey))
}
