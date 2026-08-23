export type MeetingStatus = 'draft' | 'prepared' | 'rollcall' | 'ongoing' | 'completed'
export type AttendanceStatus = 'present' | 'absent' | 'late' | 'unrecognized'

export interface PreInfo {
  date: string
  weather: string
  tasks: string
  riskPoints: string
}

export interface PlanParseResult {
  date: string
  weather: string
  tasks: string
  riskPoints: string
  city: string
}

export interface SpeechDraftDto {
  id: string
  content: string
  status: 'draft' | 'generated' | 'confirmed'
  updatedAt: string
}

export interface AttendanceItemDto {
  workerId: string
  name: string
  team: string
  status: AttendanceStatus
  confidence: number
}

export interface AttendanceRecognizeResult {
  faces: AttendanceItemDto[]
  count: number
}

export interface MeetingHistoryDto {
  id: string
  date: string
  taskPreview: string
  status: MeetingStatus
  createdAt: string
}

export interface QaRecordDto {
  id: string
  question: string
  answer: string
  intentType: 'knowledge' | 'chitchat' | 'meeting'
  sources: string[]
  createdAt: string
}

export interface ReportDto {
  id: string
  transcript: string
  attendance: AttendanceItemDto[]
  qaRecords: QaRecordDto[]
  createdAt: string
}

export interface WorkerDto {
  id: string
  name: string
  employeeNo: string
  team: string
  faceStatus: 'enrolled' | 'pending'
}

export interface WorkerCreateInput {
  name: string
  employeeNo: string
  team: string
}

export interface IdCardRecognitionDto {
  name: string
  idCardNumber: string
  gender: string
  nation: string
  birthDate: string
  address: string
  rawText: string
}

export interface KnowledgeJobStatusDto {
  state: 'processing' | 'succeeded' | 'failed' | 'partial'
  progress: number
  stage: string | null
  stageMessage: string | null
}

export interface KnowledgeUploadResult {
  docId: string
  status: KnowledgeJobStatusDto | null
}

export interface MeetingRecordDto {
  id: string
  date: string
  preInfo?: PreInfo
  preInfoJson?: string
  status: MeetingStatus
  speechDraft?: SpeechDraftDto
  attendance: AttendanceItemDto[]
  qaRecords: QaRecordDto[]
  report?: ReportDto
  createdAt: string
}
