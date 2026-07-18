import type MockAdapter from 'axios-mock-adapter'
import { standardsResult, standardsSearchHistory, standardCategories, recommendedQuestions } from '@/mock/data/standard'

export function registerStandardMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/standard/result').reply(wrap(() => standardsResult))
  mock.onGet('/api/standard/history').reply(wrap(() => standardsSearchHistory))
  mock.onGet('/api/standard/categories').reply(wrap(() => standardCategories))
  mock.onGet('/api/standard/recommended').reply(wrap(() => recommendedQuestions))
}
