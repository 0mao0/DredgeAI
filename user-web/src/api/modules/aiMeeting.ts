import request from '@/api/request'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'
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
  MeetingProjectDto,
  CreateMeetingProjectInput,
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

export interface SpeechAudioStatus {
  cached: boolean
  leadCached: boolean
  leadText: string
}

export function getSpeechAudioStatus(id: string): Promise<SpeechAudioStatus> {
  return request.get<SpeechAudioStatus>(fillUrl(urls.meetingSpeechAudioStatus, { id }))
}

export async function getSpeechLeadAudio(id: string): Promise<Blob | null> {
  try {
    return await request.get<Blob>(fillUrl(urls.meetingSpeechLeadAudio, { id }), {
      responseType: 'blob',
      timeout: MediaTimeout,
    })
  } catch {
    return null
  }
}

/** 拉取服务端按断句预热的单段语音；未缓存时返回 404（调用方回退即时合成）。 */
export function getSpeechSegmentAudio(id: string, index: number): Promise<Blob> {
  return request.get<Blob>(fillUrl(urls.meetingSpeechSegmentAudio, { id, index: String(index) }), {
    responseType: 'blob',
    timeout: MediaTimeout,
  })
}

export function saveSpeechAudioCache(id: string, audio: Blob): Promise<void> {
  const form = new FormData()
  form.append('file', audio, 'speech.wav')
  return request.post<void>(fillUrl(urls.meetingSpeechAudioCache, { id }), form, { timeout: MediaTimeout })
}

export function transcribeAudio(audio: Blob): Promise<string> {
  const form = new FormData()
  form.append('audio', audio, 'speech.wav')
  return request.post<string>(urls.meetingAsr, form, { timeout: MediaTimeout })
}

export function synthesizeSpeech(text: string, timeout = MediaTimeout): Promise<Blob> {
  return request.post<Blob>(urls.meetingTts, { text }, { responseType: 'blob', timeout })
}

/**
 * 流式 TTS：整段文本一次请求，服务端按句吐音频帧。
 * 帧格式：4 字节大端长度 + WAV；length=0 表示结束。
 */
export async function* streamSpeechAudio(text: string): AsyncGenerator<Blob> {
  const token = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_TOKEN_KEY) : null
  const res = await fetch(`${API_BASE_URL}meeting/tts/stream`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ text }),
  })
  if (!res.ok || !res.body) {
    throw new Error(`TTS 流式接口不可用（HTTP ${res.status}）`)
  }
  const reader = res.body.getReader()
  let pending = new Uint8Array(0)
  const concat = (a: Uint8Array, b: Uint8Array): Uint8Array => {
    const out = new Uint8Array(a.length + b.length)
    out.set(a, 0)
    out.set(b, a.length)
    return out
  }
  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    if (value) pending = concat(pending, value)
    while (pending.length >= 4) {
      const len = (pending[0]! << 24) | (pending[1]! << 16) | (pending[2]! << 8) | pending[3]!
      if (pending.length < 4 + len) break
      const frame = pending.slice(4, 4 + len)
      pending = pending.slice(4 + len)
      if (len === 0) return
      yield new Blob([frame], { type: 'audio/wav' })
    }
  }
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

export function uploadKnowledgeDocument(
  file: File,
  onProgress?: (percent: number) => void,
): Promise<KnowledgeUploadResult> {
  const form = new FormData()
  form.append('file', file)
  return request.post<KnowledgeUploadResult>(urls.meetingKnowledgeDocuments, form, {
    timeout: MediaTimeout,
    onUploadProgress: (e) => {
      if (onProgress && e.total) onProgress(Math.round((e.loaded / e.total) * 100))
    },
  })
}

export function getKnowledgeDocumentStatus(docId: string): Promise<KnowledgeJobStatusDto> {
  return request.get<KnowledgeJobStatusDto>(fillUrl(urls.meetingKnowledgeDocumentStatus, { docId }))
}

export function getMeetingProjects(): Promise<MeetingProjectDto[]> {
  return request.get<MeetingProjectDto[]>(urls.meetingProjects)
}

export function createMeetingProject(input: CreateMeetingProjectInput): Promise<MeetingProjectDto> {
  return request.post<MeetingProjectDto>(urls.meetingProjects, input)
}

export function updateMeetingProject(
  id: string,
  input: Pick<CreateMeetingProjectInput, 'name' | 'docIds' | 'docNames'>,
): Promise<MeetingProjectDto> {
  return request.put<MeetingProjectDto>(fillUrl(urls.meetingProject, { id }), input)
}

export function deleteMeetingProject(id: string): Promise<void> {
  return request.delete<void>(fillUrl(urls.meetingProject, { id }))
}

export function getMeetingProject(id: string): Promise<MeetingProjectDto> {
  return request.get<MeetingProjectDto>(fillUrl(urls.meetingProject, { id }))
}

export function meetingProjectDocumentFileUrl(id: string, docId: string): string {
  return fillUrl(urls.meetingProjectDocumentFile, { id, docId })
}

export async function getMeetingProjectDocumentContent(id: string, docId: string): Promise<string> {
  const data = await request.get<{ markdown: string }>(
    fillUrl(urls.meetingProjectDocumentContent, { id, docId }),
  )
  return data.markdown
}

export function extractMeetingProject(id: string): Promise<MeetingProjectDto> {
  return request.post<MeetingProjectDto>(fillUrl(urls.meetingProjectExtract, { id }), undefined, {
    timeout: MediaTimeout,
  })
}

export function suggestMeetingProjectName(docId: string): Promise<{ name: string }> {
  return request.post<{ name: string }>(urls.meetingProjectSuggestName, { docId }, {
    timeout: MediaTimeout,
  })
}

export interface UnrecognizedFaceCrop {
  blob: Blob
  confidence: number
  bbox: number[]
}

export function uploadUnrecognizedFaces(id: string, crops: UnrecognizedFaceCrop[]): Promise<number> {
  const form = new FormData()
  const metadata: Array<{ confidence: number, bbox: number[] }> = []
  crops.forEach((crop, index) => {
    form.append('files', crop.blob, `face-${index}.jpg`)
    metadata.push({ confidence: crop.confidence, bbox: crop.bbox })
  })
  form.append('metadata', JSON.stringify(metadata))
  return request.post<number>(fillUrl(urls.meetingUnrecognizedFaces, { id }), form, {
    timeout: MediaTimeout,
  })
}
