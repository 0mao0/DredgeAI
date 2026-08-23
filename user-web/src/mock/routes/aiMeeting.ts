import type MockAdapter from 'axios-mock-adapter'
import { mockWorkers, mockMeetings, createMockMeeting, generateMockSpeech } from '@shared/mock/data/aiMeeting'
import type { QaRecordDto } from '@shared/types'

export function registerMeetingMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  const meetingId = (url: string | undefined): string =>
    url?.match(/\/api\/meeting\/records\/([^/]+)\//)?.[1] ?? ''

  mock.onPost('/api/meeting/records').reply((config) => [200, createMockMeeting(JSON.parse(config.data))])
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
        createdAt: new Date().toISOString(),
      }
    }
    return [200, meeting]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/report$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.report ?? null]
  })
  mock.onGet('/api/meeting/workers').reply(wrap(() => mockWorkers))
}
