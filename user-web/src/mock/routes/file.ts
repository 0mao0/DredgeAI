import type MockAdapter from 'axios-mock-adapter'
import { fileItems } from '@shared/mock/data/file'

export function registerFileMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/file/recent').reply(wrap(() => fileItems))
}
