import type MockAdapter from 'axios-mock-adapter'
import { standardsResult, standardsSearchHistory, standardCategories, recommendedQuestions, standardList, standardProperties, standardDocuments, standardAIAnalyses } from '@shared/mock/data/standard'

export function registerStandardMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/standard/result').reply(wrap(() => standardsResult))
  mock.onGet('/api/standard/history').reply(wrap(() => standardsSearchHistory))
  mock.onGet('/api/standard/categories').reply(wrap(() => standardCategories))
  mock.onGet('/api/standard/recommended').reply(wrap(() => recommendedQuestions))
  mock.onGet('/api/standard/list').reply(wrap(() => standardList))
  mock.onGet('/api/standard/property').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardProperties.find((p) => p.id === id) || null]
  })
  mock.onGet('/api/standard/property/list').reply(wrap(() => standardProperties))
  mock.onGet('/api/standard/document').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardDocuments.find((d) => d.id === id) || null]
  })
  mock.onGet('/api/standard/ai-analysis').reply((config) => {
    const id = (config.params as Record<string, string>)?.id
    return [200, standardAIAnalyses.find((a) => a.id === id) || null]
  })
  mock.onPut('/api/standard/property').reply(wrap(() => [200, null]))
}
