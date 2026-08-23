import request from '@/api/request'
import { urls, fillUrl } from '@shared/core/api'
import type {
  MeetingRecordDto,
  MeetingHistoryDto,
  SpeechDraftDto,
  AttendanceItemDto,
  AttendanceRecognizeResult,
  QaRecordDto,
  ReportDto,
  WorkerDto,
  WorkerCreateInput,
  IdCardRecognitionDto,
  KnowledgeUploadResult,
  KnowledgeJobStatusDto,
  PlanParseResult,
  PreInfo,
} from '@/types'

/** 媒体/模型请求耗时较长（ASR 首次加载模型、TTS 合成、长音频等），放宽 axios 默认 15s 超时。 */
const MediaTimeout = 120_000

export function createMeeting(preInfo: PreInfo): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(urls.meetingRecord, preInfo)
}

export function getMeeting(id: string): Promise<MeetingRecordDto> {
  return request.get<MeetingRecordDto>(fillUrl(urls.meetingRecord, { id }))
}

export function getMeetingHistory(limit = 20): Promise<MeetingHistoryDto[]> {
  return request.get<MeetingHistoryDto[]>(urls.meetingHistory, { params: { maxCount: limit } })
}

export function parsePlan(planText: string): Promise<PlanParseResult> {
  return request.post<PlanParseResult>(urls.meetingParsePlan, { planText }, { timeout: MediaTimeout })
}

export function generateSpeech(id: string): Promise<SpeechDraftDto> {
  return request.post<SpeechDraftDto>(fillUrl(urls.meetingSpeechGenerate, { id }), undefined, { timeout: MediaTimeout })
}

export function getSpeechDraft(id: string): Promise<SpeechDraftDto | null> {
  return request.get<SpeechDraftDto | null>(fillUrl(urls.meetingSpeechDraft, { id }))
}

export function saveSpeechDraft(id: string, content: string): Promise<SpeechDraftDto> {
  return request.put<SpeechDraftDto>(fillUrl(urls.meetingSpeechDraft, { id }), { content })
}

export function getSpeechAudio(id: string): Promise<Blob> {
  return request.get<Blob>(fillUrl(urls.meetingSpeechAudio, { id }), { responseType: 'blob', timeout: MediaTimeout })
}

export function transcribeAudio(audio: Blob): Promise<string> {
  const form = new FormData()
  form.append('audio', audio, 'speech.wav')
  return request.post<string>(urls.meetingAsr, form, { timeout: MediaTimeout })
}

export function startMeeting(id: string): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingStart, { id }))
}

export function recognizeAttendance(id: string, photo: Blob): Promise<AttendanceRecognizeResult> {
  const form = new FormData()
  form.append('image', photo, 'attendance.jpg')
  return request.post<AttendanceRecognizeResult>(
    fillUrl(urls.meetingAttendanceRecognize, { id }),
    form,
    { timeout: MediaTimeout },
  )
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
  return request.post<QaRecordDto>(fillUrl(urls.meetingQaAudio, { id }), form, { timeout: MediaTimeout })
}

export function getQaAudio(qaId: string): Promise<Blob> {
  return request.get<Blob>(fillUrl(urls.meetingQaAudioGet, { qaId }), { responseType: 'blob', timeout: MediaTimeout })
}

export function uploadMeetingRecording(id: string, audio: Blob): Promise<MeetingRecordDto> {
  const form = new FormData()
  form.append('audio', audio, 'meeting.webm')
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingRecording, { id }), form, { timeout: MediaTimeout })
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

export function createWorker(input: WorkerCreateInput): Promise<WorkerDto> {
  return request.post<WorkerDto>(urls.meetingWorkers, input)
}

export function recognizeIdCard(image: Blob): Promise<IdCardRecognitionDto> {
  const form = new FormData()
  form.append('image', image, 'id-card.jpg')
  return request.post<IdCardRecognitionDto>(urls.meetingWorkersRecognizeIdCard, form)
}

export function enrollWorkerFace(id: string, photo: Blob): Promise<WorkerDto> {
  const form = new FormData()
  form.append('image', photo, 'face.jpg')
  return request.post<WorkerDto>(fillUrl(urls.meetingWorkerFace, { id }), form)
}

export function uploadKnowledgeDocument(file: File): Promise<KnowledgeUploadResult> {
  const form = new FormData()
  form.append('file', file)
  return request.post<KnowledgeUploadResult>(urls.meetingKnowledgeDocuments, form, { timeout: MediaTimeout })
}

export function getKnowledgeDocumentStatus(docId: string): Promise<KnowledgeJobStatusDto> {
  return request.get<KnowledgeJobStatusDto>(fillUrl(urls.meetingKnowledgeDocumentStatus, { docId }))
}
