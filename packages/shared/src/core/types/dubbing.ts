export interface VoiceItem {
  id: string
  name: string
  category?: '通用' | '播音' | '客服' | '解说' | '方言' | '儿童' | '播报'
  gender: '男声' | '女声' | '童声'
  style?: string
  provider?: string
  tags?: string[]
  sampleUrl?: string
  visibility: 'public' | 'private'
  userId?: string
  userName?: string
  createdAt?: string
  uploadStatus?: 'converting' | 'ready' | 'failed'
  failReason?: string
  deletedByUser?: boolean
}

export interface VoiceRegisterResult {
  voice_id: string
  name: string
  sample_url: string
  message?: string
}

export type DubbingStatus = '生成中' | '已完成' | '已失败'

export interface DubbingTask {
  id: string
  text: string
  charCount: number
  voiceId: string
  voiceName: string
  category: string
  speed: number
  status: DubbingStatus
  audioUrl?: string
  durationSec?: number
  tokenCost: number
  createdAt: string
  finishedAt?: string
  userId?: string
  userName?: string
  department?: string
  deletedByUser?: boolean
}

export interface DubbingUsageTimeSeries {
  categories: string[]
  tasks: { name: string; data: number[] }[]
  tokens: { name: string; data: number[] }[]
  users: { name: string; data: number[] }[]
}

export interface DubbingUsageSummary {
  totalTasks: number
  totalTokens: number
  totalUsers: number
  totalAudioSec: number
  todayTasks: number
  todayTokens: number
}
