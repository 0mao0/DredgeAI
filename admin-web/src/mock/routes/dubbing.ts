import type MockAdapter from 'axios-mock-adapter'
import { dubbingTasks, dubbingUsageSummary, dubbingUsageTimeSeries, voiceItems } from '@shared/mock/data/dubbing'
import type { VoiceItem } from '@/types'

// Public voices from shared mock data
const publicVoices: VoiceItem[] = voiceItems.map((v) => ({ ...v, visibility: 'public' as const }))

// Simulated private voices uploaded by users (some soft-deleted)
const privateVoices: VoiceItem[] = [
  {
    id: 'private_voice_001',
    name: '我的声音16:44',
    gender: '男声',
    provider: '自定义',
    visibility: 'private',
    userId: 'local_user',
    userName: '张建国',
    createdAt: '2026-07-22T08:44:00',
    uploadStatus: 'ready',
  },
  {
    id: 'private_voice_002',
    name: '测试录音-项目汇报',
    gender: '女声',
    provider: '自定义',
    visibility: 'private',
    userId: 'u-002',
    userName: '李小梅',
    createdAt: '2026-07-21T14:30:00',
    uploadStatus: 'ready',
  },
  {
    id: 'private_voice_003',
    name: '我的朗读声音',
    gender: '男声',
    provider: '自定义',
    visibility: 'private',
    userId: 'u-001',
    userName: '张建国',
    createdAt: '2026-07-20T10:15:00',
    uploadStatus: 'ready',
    deletedByUser: true,
  },
  {
    id: 'private_voice_004',
    name: '会议录音-陈经理',
    gender: '男声',
    provider: '自定义',
    visibility: 'private',
    userId: 'u-005',
    userName: '陈晓东',
    createdAt: '2026-07-19T16:00:00',
    uploadStatus: 'failed',
    failReason: '服务器处理超时',
    deletedByUser: true,
  },
]

const adminVoices: VoiceItem[] = [...publicVoices, ...privateVoices]

export function registerDubbingMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/dubbing/admin/tasks').reply((config) => {
    const params = config.params || {}
    let items = [...dubbingTasks]
    if (params.keyword) {
      const kw = String(params.keyword).toLowerCase()
      items = items.filter((t) => (t.userName || '').toLowerCase().includes(kw) || t.text.toLowerCase().includes(kw))
    }
    if (params.status) {
      items = items.filter((t) => t.status === params.status)
    }
    if (params.deletedOnly) {
      items = items.filter((t) => t.deletedByUser)
    }
    return [200, { items, totalCount: items.length }]
  })

  mock.onDelete(/\/api\/admin\/dubbing\/admin\/tasks\/(.+)$/).reply((config) => {
    const match = config.url?.match(/\/api\/admin\/dubbing\/admin\/tasks\/(.+)$/)
    if (!match) return [404, {}]
    const id = match[1]
    const idx = dubbingTasks.findIndex((t) => t.id === id)
    if (idx === -1) return [404, {}]
    if (!dubbingTasks[idx].deletedByUser) {
      return [403, {
        error: {
          code: null,
          message: '用户未删除，受隐私限制不可彻底删除',
          details: null,
          data: null,
          validationErrors: null,
        },
      }]
    }
    dubbingTasks.splice(idx, 1)
    return [204]
  })

  mock.onGet('/api/admin/dubbing/admin/voices').reply((config) => {
    const params = config.params || {}
    let list = [...adminVoices]
    if (params.keyword) {
      const kw = String(params.keyword).toLowerCase()
      list = list.filter((v) => v.name.toLowerCase().includes(kw) || (v.userName || '').toLowerCase().includes(kw))
    }
    if (params.deletedOnly) {
      list = list.filter((v) => v.deletedByUser)
    }
    return [200, list]
  })

  mock.onPost('/api/admin/dubbing/admin/voices').reply((config) => {
    const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data
    const now = new Date().toISOString()
    const newVoice: VoiceItem = {
      id: `admin_voice_${Date.now()}`,
      name: body.name || '管理员音色',
      gender: body.gender || '男声',
      provider: '自定义',
      visibility: 'public',
      sampleUrl: `/tts/samples/admin_voice_${Date.now()}.wav`,
      createdAt: now,
    }
    adminVoices.unshift(newVoice)
    return [200, newVoice]
  })

  mock.onDelete(/\/api\/admin\/dubbing\/admin\/voices\/(.+)$/).reply((config) => {
    const match = config.url?.match(/\/api\/admin\/dubbing\/admin\/voices\/(.+)$/)
    if (!match) return [404, {}]
    const id = match[1]
    const idx = adminVoices.findIndex((v) => v.id === id)
    if (idx === -1) return [404, {}]
    const voice = adminVoices[idx]
    // Public voices can always be deleted; private only if user already deleted
    if (voice.visibility === 'private' && !voice.deletedByUser) {
      return [403, { message: '用户未删除，受隐私限制不可彻底删除' }]
    }
    adminVoices.splice(idx, 1)
    return [204]
  })

  mock.onGet('/api/admin/dubbing/admin/usage/summary').reply(wrap(() => dubbingUsageSummary))

  mock.onGet('/api/admin/dubbing/admin/usage/timeseries').reply(wrap(() => dubbingUsageTimeSeries))
}
