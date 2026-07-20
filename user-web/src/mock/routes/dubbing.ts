import type MockAdapter from 'axios-mock-adapter'
import { voiceItems, dubbingTasks } from '@shared/mock/data/dubbing'
import type { DubbingStatus, DubbingTask } from '@/types'
import sampleUrl from '@shared/assets/dubbing-sample.mp3'

let nextId = 100

function buildTask(overrides: Partial<DubbingTask> & { id: string; text: string; status: DubbingStatus }): DubbingTask {
  return {
    charCount: overrides.text.length,
    tokenCost: Math.ceil(overrides.text.length / 1.5) + 50,
    voiceId: 'zh-female-general',
    voiceName: '知柔·女声',
    category: '通用',
    speed: 1.0,
    createdAt: new Date().toISOString(),
    ...overrides,
  }
}

export function registerDubbingMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/dubbing/voices').reply(wrap(() => voiceItems))

  mock.onPost('/api/dubbing/generate').reply((config) => {
    const { text, voiceId, speed } = JSON.parse(config.data)
    const voice = voiceItems.find((v) => v.id === voiceId)
    const id = `dubbing-${nextId++}`

    const newTask = buildTask({
      id,
      text,
      voiceId,
      voiceName: voice?.name || '未知',
      category: voice?.category || '通用',
      speed: speed || 1.0,
      status: '生成中',
    })

    dubbingTasks.unshift(newTask)

    setTimeout(() => {
      const task = dubbingTasks.find((t) => t.id === id)
      if (task) {
        task.status = '已完成'
        task.audioUrl = sampleUrl
        task.durationSec = Math.round((text.length / 4 / Math.max(speed || 1, 0.1)) * 10) / 10
        task.finishedAt = new Date().toISOString()
      }
    }, 1500)

    return [200, newTask]
  })

  mock.onGet('/api/dubbing/tasks').reply(wrap(() => {
    const filtered = dubbingTasks.filter((t) => t.deletedByUser !== true)
    return { items: filtered, totalCount: filtered.length }
  }))

  mock.onGet(new RegExp('/api/dubbing/tasks/([^/]+)$')).reply((config) => {
    const match = config.url?.match(/\/api\/dubbing\/tasks\/([^/]+)$/)
    const id = match?.[1] || ''
    const task = dubbingTasks.find((t) => t.id === id)
    return task ? [200, task] : [404, { message: 'Task not found' }]
  })

  mock.onDelete(new RegExp('/api/dubbing/tasks/([^/]+)$')).reply((config) => {
    const match = config.url?.match(/\/api\/dubbing\/tasks\/([^/]+)$/)
    const id = match?.[1] || ''
    const task = dubbingTasks.find((t) => t.id === id)
    if (task) task.deletedByUser = true
    return [204, undefined]
  })

  mock.onGet(new RegExp('/api/dubbing/tasks/([^/]+)/download$')).reply((config) => {
    const match = config.url?.match(/\/api\/dubbing\/tasks\/([^/]+)/)
    const id = match?.[1] || ''
    const task = dubbingTasks.find((t) => t.id === id)
    return [200, { url: task?.audioUrl || sampleUrl }]
  })
}
