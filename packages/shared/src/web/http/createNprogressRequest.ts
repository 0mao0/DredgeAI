import { message } from 'ant-design-vue'
import { createWebRequest } from './createWebRequest'
import type { CreateWebRequestOptions } from './createWebRequest'
import type { RequestInstance } from '@shared/core/http/types'

/**
 * 预置 antd 全局错误 toast 的 request 工厂。
 * 双端共用，各端仅需传入 baseURL / tokenKey / onUnauthorized。
 */
export function createNprogressRequest(
  opts: Omit<CreateWebRequestOptions, 'onProgressStart' | 'onProgressDone' | 'onError'>,
): RequestInstance {
  return createWebRequest({
    ...opts,
    onProgressStart: () => {},
    onProgressDone: () => {},
    onError: (msg) => { message.error(msg) },
  })
}
