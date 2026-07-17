import type MockAdapter from 'axios-mock-adapter'
import { fileItems } from '@/mock/file'

export function registerFileMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/file/recent').reply(wrap(() => fileItems))
}
