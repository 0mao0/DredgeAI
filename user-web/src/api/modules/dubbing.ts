import axios from 'axios'
import { urls } from '@shared/core/api'
import type { VoiceItem, VoiceRegisterResult } from '@/types'

/**
 * TTS 服务专属请求实例。
 * 直连 CosyVoice 服务（开发期经 Vite /tts 代理转发到 localhost:8000），
 * 不挂载 ABP 拦截器，不经 mock，避免与现有 /api 后端混用。
 * 标准 axios 实例，返回标准 AxiosResponse 类型。
 */
const ttsClient = axios.create({
  baseURL: '',
  timeout: 120000,
})

export { ttsClient }

export async function getVoices(): Promise<VoiceItem[]> {
  const res = await ttsClient.get<VoiceItem[]>(urls.dubbingVoices)
  return res.data
}

/** 同步合成：POST 文本，返回音频二进制流（wav） */
export async function generateDubbing(text: string, voiceId: string, speed: number): Promise<Blob> {
  const res = await ttsClient.post<Blob>(
    urls.dubbingGenerate,
    { text, voice_id: voiceId, speed },
    { responseType: 'blob' },
  )
  return res.data
}

/** 注册新音色：上传录音/文件，返回音色信息（模型推理样本耗时较长，超时 5 分钟） */
export async function registerVoice(formData: FormData): Promise<VoiceRegisterResult> {
  const res = await ttsClient.post<VoiceRegisterResult>(
    urls.dubbingRegister,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 300000,
    },
  )
  return res.data
}
