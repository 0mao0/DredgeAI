export type MeetingStatus = 'draft' | 'prepared' | 'rollcall' | 'ongoing' | 'completed'
export type AttendanceStatus = 'present' | 'absent' | 'late' | 'unrecognized'

export interface PreInfo {
  date: string
  weather: string
  tasks: string
  riskPoints: string
  /** 所选施工项目名称（晨会稿生成引用项目上下文） */
  projectName?: string
  /** 所选项目施工方案提取的主要内容 */
  projectSummary?: string
}

export interface MeetingProjectDto {
  id: string
  name: string
  anGineerDocId?: string | null
  docIds: string[]
  /** 与 docIds 对齐的原始文件名 */
  docNames: string[]
  status: 'processing' | 'ready' | 'failed'
  projectInfoJson: string
  summary: string
  createdAt: string
}

export interface CreateMeetingProjectInput {
  name: string
  docId: string
  docIds?: string[]
  docNames?: string[]
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
  workerId?: string | null
  name: string
  team: string
  status: AttendanceStatus
  confidence: number
  /** 人脸框 [x1, y1, x2, y2]，未识别人脸去重/后续人脸入库用 */
  bbox?: number[]
  /** 工人证件号（身份证号），同名时用于展示“姓名-生日后四位”区分 */
  employeeNo?: string
  /** 工人人脸照片访问地址 */
  facePhotoUrl?: string
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
  unrecognizedFaces: UnrecognizedFaceDto[]
  createdAt: string
}

export interface UnrecognizedFaceDto {
  id: string
  photoUrl: string
  confidence: number
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
