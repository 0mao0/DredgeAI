import type { MeetingRecordDto, SpeechDraftDto, WorkerDto } from '@shared/types'

export const mockWorkers: WorkerDto[] = [
  { id: 'w-001', name: '张建国', employeeNo: 'A001', team: '钢筋班', faceStatus: 'enrolled' },
  { id: 'w-002', name: '李大海', employeeNo: 'A002', team: '模板班', faceStatus: 'enrolled' },
  { id: 'w-003', name: '王强', employeeNo: 'A003', team: '电工班', faceStatus: 'pending' },
]

export const mockMeetings: MeetingRecordDto[] = []
let nextMeetingId = 1

export function createMockMeeting(preInfo: MeetingRecordDto['preInfo']): MeetingRecordDto {
  const id = `meeting-${nextMeetingId++}`
  const meeting: MeetingRecordDto = {
    id,
    date: preInfo.date,
    preInfo,
    status: 'draft',
    attendance: [],
    qaRecords: [],
    createdAt: new Date().toISOString(),
  }
  mockMeetings.push(meeting)
  return meeting
}

export function generateMockSpeech(id: string): SpeechDraftDto {
  const meeting = mockMeetings.find((m) => m.id === id)
  const p = meeting?.preInfo
  const draft: SpeechDraftDto = {
    id: `speech-${id}`,
    content:
      `各位工友早上好！今天是${p?.date ?? ''}，天气${p?.weather ?? '晴'}。\n`
      + `今日任务：${p?.tasks ?? '按计划施工'}。\n`
      + `风险提示：${p?.riskPoints ?? '注意安全'}。\n`
      + `请各班组长核对人员，戴好安全帽，开始今天的工作。`,
    status: 'generated',
    updatedAt: new Date().toISOString(),
  }
  if (meeting) meeting.speechDraft = draft
  return draft
}
