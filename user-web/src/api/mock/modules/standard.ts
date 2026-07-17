import type MockAdapter from 'axios-mock-adapter'
import { standardsResult, standardsSearchHistory, standardCategories, recommendedQuestions } from '@/mock/standard'

export function registerStandardMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/standard/result').reply(wrap(() => standardsResult))
  mock.onGet('/standard/history').reply(wrap(() => standardsSearchHistory))
  mock.onGet('/standard/categories').reply(wrap(() => standardCategories))
  mock.onGet('/standard/recommended').reply(wrap(() => recommendedQuestions))
}
