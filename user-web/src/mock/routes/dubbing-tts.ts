import type MockAdapter from 'axios-mock-adapter'
import { voiceItems } from '@shared/mock/data/dubbing'
import type { VoiceItem, VoiceRegisterResult } from '@/types'

const PRIVATE_VOICES_KEY = 'DREDGE_AI_PRIVATE_VOICES'

function getPrivateVoices(): VoiceItem[] {
  try {
    return JSON.parse(localStorage.getItem(PRIVATE_VOICES_KEY) || '[]')
  } catch {
    return []
  }
}

function savePrivateVoice(voice: VoiceItem): void {
  const list = getPrivateVoices().filter(v => v.id !== voice.id)
  list.unshift(voice)
  localStorage.setItem(PRIVATE_VOICES_KEY, JSON.stringify(list))
}

export function registerDubbingTtsMock(mock: MockAdapter): void {
  mock.onGet('/tts/voices').reply(async () => {
    const publicVoices = voiceItems.map(v => ({ ...v, visibility: 'public' as const }))
    const privateVoices = getPrivateVoices()
    return [200, [...privateVoices, ...publicVoices]]
  })

  mock.onPost('/tts/voices/upload').reply(async (config) => {
    const body = config.data as FormData
    const name = body?.get('name') as string || '我的音色'
    const gender = body?.get('gender') as string || '男声'

    const voiceId = `voice_${Date.now()}`
    const now = new Date().toISOString()
    const voice: VoiceItem = {
      id: voiceId,
      name,
      category: '通用',
      gender: gender as VoiceItem['gender'],
      style: '自定义音色',
      provider: '自定义',
      visibility: 'private',
      userId: 'local_user',
      sampleUrl: `/tts/samples/${voiceId}.wav`,
      createdAt: now,
    }
    savePrivateVoice(voice)

    const result: VoiceRegisterResult = {
      voice_id: voiceId,
      name: voice.name,
      sample_url: `/api/samples/${voiceId}.wav`,
      message: `Voice "${voice.name}" registered successfully.`,
    }
    return [200, result]
  })

}
