import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { QaRecordDto } from '@/types'

import { getQaAudio } from '@/api/modules/aiMeeting'
import { useQaAudio } from '@/views/ai-meeting/composables/useQaAudio'

vi.mock('@/api/modules/aiMeeting', () => ({
  getQaAudio: vi.fn(),
}))

function makeQa(id: string): QaRecordDto {
  return {
    id,
    question: 'q',
    answer: 'a',
    intentType: 'chitchat',
    sources: [],
    createdAt: new Date(),
  }
}

describe('useQaAudio', () => {
  beforeEach(() => {
    vi.mocked(getQaAudio).mockReset()
  })

  it('语音提问后新问答记录到达时自动拉取并播放答案音频', async () => {
    const records = ref<QaRecordDto[]>([])
    const play = vi.fn()
    const { pendingVoice } = useQaAudio(records, play)
    vi.mocked(getQaAudio).mockResolvedValue(new Blob(['wav']))

    pendingVoice.value = true
    records.value = [makeQa('qa-1')]

    await vi.waitFor(() => expect(getQaAudio).toHaveBeenCalledWith('qa-1'))
    expect(play).toHaveBeenCalled()
    expect(pendingVoice.value).toBe(false)
  })

  it('playById 手动重播指定答案', async () => {
    const records = ref<QaRecordDto[]>([])
    const play = vi.fn()
    const { playById } = useQaAudio(records, play)
    vi.mocked(getQaAudio).mockResolvedValue(new Blob(['wav']))

    await playById('qa-2')

    expect(getQaAudio).toHaveBeenCalledWith('qa-2')
    expect(play).toHaveBeenCalled()
  })
})
