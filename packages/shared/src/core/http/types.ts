import type { AxiosInstance, AxiosRequestConfig } from 'axios'

/** 创建 request 实例的参数 */
export interface CreateRequestOptions {
  /** axios baseURL，例如 '/api' 或 '/api/admin' */
  baseURL: string
  /** localStorage 中存储 token 的 key */
  tokenKey: string
  /** 请求超时毫秒，默认 15000 */
  timeout?: number
  /** 401 未授权时的回调（可选，由各端注入跳转逻辑） */
  onUnauthorized?: () => void
}

/**
 * 工厂返回的请求实例：泛型方法直接返回数据体（ABP 拦截器已解包 AxiosResponse）。
 * 独立包装接口，替代旧的 `declare module 'axios'` 全局扩展，避免污染第三方库类型。
 */
export interface RequestInstance {
  get: <T = unknown>(url: string, config?: AxiosRequestConfig) => Promise<T>
  post: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) => Promise<T>
  put: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) => Promise<T>
  patch: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) => Promise<T>
  delete: <T = unknown>(url: string, config?: AxiosRequestConfig) => Promise<T>
  /** 拦截器（供 createWebRequest 等包装层挂载进度/错误处理） */
  interceptors: AxiosInstance['interceptors']
  /** 逃生舱：底层 axios 实例，仅供 MockAdapter 挂载等场景，业务代码禁止使用 */
  raw: AxiosInstance
}
