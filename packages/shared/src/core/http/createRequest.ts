import axios from 'axios'
import type { CreateRequestOptions, RequestInstance } from './types'
import { applyAbpInterceptors } from './abp'

/**
 * 创建带 ABP 协议拦截器的 axios 实例。
 * 框架无关：不依赖 Vue / nprogress，进度条由 web 端包装器处理。
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

  return instance as RequestInstance
}
