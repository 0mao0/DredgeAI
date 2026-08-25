import type MockAdapter from 'axios-mock-adapter'
import { mockWorkers, mockMeetings, createMockMeeting, generateMockSpeech } from '@shared/mock/data/aiMeeting'
import type { QaRecordDto } from '@shared/types'

export function registerMeetingMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  const meetingId = (url: string | undefined): string =>
    url?.match(/\/meeting\/records\/([^/]+)\//)?.[1] ?? ''

  mock.onPost('/api/meeting/records').reply((config) => [200, createMockMeeting(JSON.parse(config.data))])
  mock.onPost('/api/meeting/parse-plan').reply((config) => {
    const text = JSON.parse(config.data).planText ?? ''
    return [
      200,
      {
        date: new Date().toISOString().slice(0, 10),
        weather: '多云 26℃',
        tasks: text || '（未输入）',
        riskPoints: '高处作业、临边防护',
        city: '上海',
      },
    ]
  })
  mock.onGet('/api/meeting/records').reply((config) => {
    const limit = Number(config.params?.maxCount ?? 20)
    return [
      200,
      [...mockMeetings]
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
        .slice(0, limit)
        .map((m) => ({
          id: m.id,
          date: m.date,
          taskPreview: m.preInfo?.tasks ?? '',
          status: m.status,
          createdAt: m.createdAt,
        })),
    ]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/speech\/audio$/).reply(() => {
    // 1 秒静音 WAV（44 字节头 + 静音 PCM），让前端播放链路可跑通
    const sampleRate = 8000
    const seconds = 1
    const dataSize = sampleRate * seconds
    const buffer = new ArrayBuffer(44 + dataSize)
    const view = new DataView(buffer)
    const writeAscii = (offset: number, text: string): void => {
      for (let i = 0; i < text.length; i++) view.setUint8(offset + i, text.charCodeAt(i))
    }
    writeAscii(0, 'RIFF')
    view.setUint32(4, 36 + dataSize, true)
    writeAscii(8, 'WAVE')
    writeAscii(12, 'fmt ')
    view.setUint32(16, 16, true)
    view.setUint16(20, 1, true)
    view.setUint16(22, 1, true)
    view.setUint32(24, sampleRate, true)
    view.setUint32(28, sampleRate, true)
    view.setUint16(32, 1, true)
    view.setUint16(34, 8, true)
    writeAscii(36, 'data')
    view.setUint32(40, dataSize, true)
    return [200, new Blob([buffer], { type: 'audio/wav' })]
  })
  mock.onPost('/api/meeting/asr').reply(() => [
    200,
    '今日任务：基坑支护施工与临边防护检查，注意高处作业安全。',
  ])
  mock.onPost('/api/meeting/knowledge/documents').reply(() => [
    200,
    { docId: `doc-${Date.now()}`, status: { state: 'succeeded', progress: 100, stage: 'done', stageMessage: null } },
  ])
  mock.onGet(/\/api\/meeting\/knowledge\/documents\/[^/]+\/status$/).reply(() => [
    200,
    { state: 'succeeded', progress: 100, stage: 'done', stageMessage: null },
  ])
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/speech\/generate$/).reply((config) => {
    return [200, generateMockSpeech(meetingId(config.url))]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/speech$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.speechDraft ?? null]
  })
  mock.onPut(/\/api\/meeting\/records\/[^/]+\/speech$/).reply((config) => {
    const meeting = mockMeetings.find((m) => m.id === meetingId(config.url))
    if (meeting?.speechDraft) {
      meeting.speechDraft.content = JSON.parse(config.data).content
      meeting.speechDraft.status = 'confirmed'
    }
    return [200, meeting?.speechDraft ?? null]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/start$/).reply((config) => {
    const meeting = mockMeetings.find((m) => m.id === meetingId(config.url))
    if (meeting) meeting.status = 'rollcall'
    return [200, meeting]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/attendance\/recognize$/).reply((config) => {
    const meeting = mockMeetings.find((m) => m.id === meetingId(config.url))
    if (meeting) {
      meeting.attendance = [
        { workerId: 'w-001', name: '张建国', team: '钢筋班', status: 'present', confidence: 0.96 },
        { workerId: 'w-002', name: '李大海', team: '模板班', status: 'present', confidence: 0.92 },
      ]
    }
    return [200, { faces: meeting?.attendance ?? [] }]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/attendance$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.attendance ?? []]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/qa$/).reply((config) => {
    const id = meetingId(config.url)
    const q = JSON.parse(config.data).question
    const rec: QaRecordDto = {
      id: `qa-${Date.now()}`,
      question: q,
      answer: '根据知识库检索，请正确佩戴安全帽并遵守现场安全规程。',
      intentType: 'knowledge',
      sources: ['mock-source'],
      createdAt: new Date().toISOString(),
    }
    mockMeetings.find((m) => m.id === id)?.qaRecords.push(rec)
    return [200, rec]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/qa\/audio$/).reply((config) => {
    const id = meetingId(config.url)
    const rec: QaRecordDto = {
      id: `qa-${Date.now()}`,
      question: '（语音）今天需要注意什么？',
      answer: '今日重点注意高处作业与临边防护。',
      intentType: 'meeting',
      sources: [],
      createdAt: new Date().toISOString(),
    }
    mockMeetings.find((m) => m.id === id)?.qaRecords.push(rec)
    return [200, rec]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/recording$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url)) ?? null]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/complete$/).reply((config) => {
    const id = meetingId(config.url)
    const meeting = mockMeetings.find((m) => m.id === id)
    if (meeting) {
      meeting.status = 'completed'
      meeting.report = {
        id: `report-${id}`,
        transcript: '（转写稿）各位工友早上好……',
        attendance: meeting.attendance,
        qaRecords: meeting.qaRecords,
        unrecognizedFaces: [],
        createdAt: new Date().toISOString(),
      }
    }
    return [200, meeting]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/report$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.report ?? null]
  })
  mock.onGet('/api/meeting/workers').reply(wrap(() => mockWorkers))
  mock.onPost('/api/meeting/workers').reply((config) => {
    const input = JSON.parse(config.data)
    const existing = mockWorkers.find((w) => w.employeeNo === input.employeeNo)
    if (existing) return [200, existing]
    const worker = {
      id: `w-${Date.now()}`,
      name: input.name,
      employeeNo: input.employeeNo,
      team: input.team ?? '',
      faceStatus: 'pending' as const,
    }
    mockWorkers.push(worker)
    return [200, worker]
  })
  mock.onPost('/api/meeting/workers/recognize-id-card').reply(() => [
    200,
    {
      name: '张建国',
      idCardNumber: '110101199001011234',
      gender: '男',
      nation: '汉',
      birthDate: '1990-01-01',
      address: '北京市朝阳区',
      rawText: '',
    },
  ])
  mock.onPost(/\/api\/meeting\/workers\/[^/]+\/face$/).reply((config) => {
    const id = config.url?.match(/\/api\/meeting\/workers\/([^/]+)\/face$/)?.[1] ?? ''
    const worker = mockWorkers.find((w) => w.id === id)
    if (worker) worker.faceStatus = 'enrolled'
    return [200, worker ?? mockWorkers[0]]
  })
}
