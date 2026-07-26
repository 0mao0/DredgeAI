import axios from 'axios'
import type { AxiosRequestConfig, AxiosResponse } from 'axios'
import type { CreateRequestOptions, RequestInstance } from './types'
import { applyAbpInterceptors } from './abp'

/**
 * 创建带 ABP 协议拦截器的请求实例。
 * 框架无关：不依赖 Vue / nprogress，进度条由 web 端包装器处理。
 * 返回独立 RequestInstance 包装（泛型方法直接返回数据体），不污染全局 axios 类型。
 */
export function createRequest(opts: CreateRequestOptions): RequestInstance {
  const { baseURL, tokenKey, timeout = 15000, onUnauthorized } = opts

  const instance = axios.create({ baseURL, timeout })

  // token 注入
  instance.interceptors.request.use((config) => {
    const token = typeof localStorage !== 'undefined' ? localStorage.getItem(tokenKey) : null
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  })

  applyAbpInterceptors(instance, { onUnauthorized })

  // ABP 响应拦截器已将数据解包（response.data），此处仅做类型收敛
  const unwrap = <T>(p: Promise<AxiosResponse<T>>): Promise<T> => p as unknown as Promise<T>

  return {
    get: <T = unknown>(url: string, config?: AxiosRequestConfig) =>
      unwrap<T>(instance.get<T>(url, config)),
    post: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
      unwrap<T>(instance.post<T>(url, data, config)),
    put: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
      unwrap<T>(instance.put<T>(url, data, config)),
    patch: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
      unwrap<T>(instance.patch<T>(url, data, config)),
    delete: <T = unknown>(url: string, config?: AxiosRequestConfig) =>
      unwrap<T>(instance.delete<T>(url, config)),
    interceptors: instance.interceptors,
    raw: instance,
  }
}
