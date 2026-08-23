import { ref, watch } from 'vue'
import type { Ref } from 'vue'
import type { QaRecordDto } from '@/types'
import { getQaAudio } from '@/api/modules/aiMeeting'

/**
 * 问答答案语音播放：语音提问后新记录到达自动播报；任意答案可手动重播。
 * 依赖 meeting-bot TTS（后端 GET /api/meeting/qa/{id}/audio 返回 WAV）。
 */
export function useQaAudio(
  qaRecords: Ref<QaRecordDto[]>,
  play: (blob: Blob) => void,
) {
  /** 语音提问待播标记：下一条新问答记录到达时自动播报。 */
  const pendingVoice = ref(false)
  /** 正在合成/播放的问答 id（用于按钮 loading 态）。 */
  const playingId = ref<string | null>(null)

  watch(
    () => qaRecords.value.length,
    (len, prevLen) => {
      if (pendingVoice.value && len > prevLen) {
        pendingVoice.value = false
        void playById(qaRecords.value[qaRecords.value.length - 1]!.id)
      }
    },
  )

  async function playById(qaId: string): Promise<void> {
    playingId.value = qaId
    try {
      const blob = await getQaAudio(qaId)
      play(blob)
    } finally {
      playingId.value = null
    }
  }

  return { pendingVoice, playingId, playById }
}
