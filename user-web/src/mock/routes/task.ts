import type MockAdapter from 'axios-mock-adapter'
import { taskItems, quickTasks } from '@shared/mock/data/task'

export function registerTaskMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/task/recent').reply(wrap(() => taskItems))
  mock.onGet('/api/task/quick').reply(wrap(() => quickTasks))
}
