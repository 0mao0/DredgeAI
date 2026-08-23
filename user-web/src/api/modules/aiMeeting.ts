import request from '@/api/request'
import { urls, fillUrl } from '@shared/core/api'
import type {
  MeetingRecordDto,
  SpeechDraftDto,
  AttendanceItemDto,
  QaRecordDto,
  ReportDto,
  WorkerDto,
  PreInfo,
} from '@/types'

export function createMeeting(preInfo: PreInfo): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(urls.meetingRecord, preInfo)
}

export function generateSpeech(id: string): Promise<SpeechDraftDto> {
  return request.post<SpeechDraftDto>(fillUrl(urls.meetingSpeechGenerate, { id }))
}

export function getSpeechDraft(id: string): Promise<SpeechDraftDto | null> {
  return request.get<SpeechDraftDto | null>(fillUrl(urls.meetingSpeechDraft, { id }))
}

export function saveSpeechDraft(id: string, content: string): Promise<SpeechDraftDto> {
  return request.put<SpeechDraftDto>(fillUrl(urls.meetingSpeechDraft, { id }), { content })
}

export function startMeeting(id: string): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingStart, { id }))
}

export function recognizeAttendance(id: string, photo: Blob): Promise<{ faces: AttendanceItemDto[] }> {
  const form = new FormData()
  form.append('image', photo, 'attendance.jpg')
  return request.post<{ faces: AttendanceItemDto[] }>(fillUrl(urls.meetingAttendanceRecognize, { id }), form)
}

export function getAttendance(id: string): Promise<AttendanceItemDto[]> {
  return request.get<AttendanceItemDto[]>(fillUrl(urls.meetingAttendance, { id }))
}

export function askQa(id: string, question: string): Promise<QaRecordDto> {
  return request.post<QaRecordDto>(fillUrl(urls.meetingQa, { id }), { question })
}

export function askQaAudio(id: string, audio: Blob): Promise<QaRecordDto> {
  const form = new FormData()
  form.append('audio', audio, 'question.webm')
  return request.post<QaRecordDto>(fillUrl(urls.meetingQaAudio, { id }), form)
}

export function uploadMeetingRecording(id: string, audio: Blob): Promise<MeetingRecordDto> {
  const form = new FormData()
  form.append('audio', audio, 'meeting.webm')
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingRecording, { id }), form)
}

export function completeMeeting(id: string): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingComplete, { id }))
}

export function getReport(id: string): Promise<ReportDto | null> {
  return request.get<ReportDto | null>(fillUrl(urls.meetingReport, { id }))
}

export function getWorkers(): Promise<WorkerDto[]> {
  return request.get<WorkerDto[]>(urls.meetingWorkers)
}
