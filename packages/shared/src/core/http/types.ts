import type { AxiosInstance } from 'axios'

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

/** 工厂返回的 axios 实例（已注入 ABP 拦截器与泛型方法签名） */
export type RequestInstance = AxiosInstance

/** 模块级 axios 实例扩展声明：注入泛型 get/post/put/patch/delete */
declare module 'axios' {
  export interface AxiosInstance {
    get<T = unknown>(url: string, config?: import('axios').AxiosRequestConfig): Promise<T>
    post<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
    put<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
    patch<T = unknown>(url: string, data?: unknown, config?: import('axios').AxiosRequestConfig): Promise<T>
    delete<T = unknown>(url: string, config?: import('axios').AxiosRequestConfig): Promise<T>
  }
}
