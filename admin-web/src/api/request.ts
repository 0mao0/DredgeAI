import axios from 'axios'
import type { AxiosResponse } from 'axios'
import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { API_BASE_URL } from '@/utils/constants'

export interface ApiResponse<T = unknown> {
  code: number
  data: T
  message: string
}

const request = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
})

request.interceptors.request.use((config) => {
  nprogress.start()
  return config
})

request.interceptors.response.use(
  (response: AxiosResponse<ApiResponse>) => {
    nprogress.done()
    const res = response.data
    if (res.code !== 0) {
      return Promise.reject(new Error(res.message || '请求失败'))
    }
    return res.data as unknown as AxiosResponse
  },
  (error) => {
    nprogress.done()
    return Promise.reject(error)
  },
)

export default request

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
