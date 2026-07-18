import axios from 'axios'
import type { AxiosResponse } from 'axios'
import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

/** ABP 错误响应结构 */
export interface AbpErrorInfo {
  code: string | null
  message: string | null
  details: string | null
  data: Record<string, unknown> | null
  validationErrors: Array<{ message: string | null; members: string[] | null }> | null
}

export interface AbpErrorResponse {
  error: AbpErrorInfo
}

/** 分页查询响应 */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
}

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}

const request = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
})

request.interceptors.request.use((config) => {
  nprogress.start()
  const token = localStorage.getItem(STORAGE_TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

request.interceptors.response.use(
  (response: AxiosResponse) => {
    nprogress.done()
    // ABP 格式：成功响应直接返回数据体
    return response.data
  },
  (error) => {
    nprogress.done()
    // ABP 格式：错误响应提取 error 信息
    const abpError: AbpErrorInfo | undefined = error.response?.data?.error
    if (abpError) {
      return Promise.reject(new Error(abpError.message || '请求失败'))
    }
    return Promise.reject(error)
  },
)

declare module 'axios' {
  export interface AxiosInstance {
    get<T = unknown>(url: string, config?: import('axios').AxiosRequestConfig): Promise<T>
    post<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
    put<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
    patch<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
    delete<T = unknown>(url: string, config?: import('axios').AxiosRequestConfig): Promise<T>
  }
}

export default request