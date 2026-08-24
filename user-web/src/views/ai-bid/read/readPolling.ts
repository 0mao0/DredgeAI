/**
 * 读标任务轮询状态机（纯逻辑，便于回归测试）。
 *
 * 轮询态：uploading / parsing / parsed / extracting / reviewing；
 * 终态：ready / partial / failed 等。
 */

export const READ_POLLING_STATUSES = [
  'uploading',
  'parsing',
  'parsed',
  'extracting',
  'reviewing',
] as const

export type ReadPollingStatus = (typeof READ_POLLING_STATUSES)[number]

export function isReadPollingStatus(status: string | null | undefined): status is ReadPollingStatus {
  return !!status && (READ_POLLING_STATUSES as readonly string[]).includes(status)
}

export interface ReadTaskStatusHandlers {
  /** 任务被清空（task 置 null）时调用 */
  onCleared: () => void
  /** 进入 parsed：启动“解析完成但抽取未启动”看门狗 */
  onParsed: () => void
  /** 离开 parsed */
  onLeftParsed: () => void
  /** 进入轮询态：开始轮询 */
  onStartPolling: () => void
  /** 到达终态：停止轮询并补拉一轮完整数据（基准库/解析产物） */
  onTerminal: () => void
}

export function applyReadTaskStatus(
  status: string | null | undefined,
  handlers: ReadTaskStatusHandlers,
): void {
  if (!status) {
    handlers.onCleared()
    return
  }

  if (status === 'parsed') {
    handlers.onParsed()
  } else {
    handlers.onLeftParsed()
  }

  if (isReadPollingStatus(status)) {
    handlers.onStartPolling()
  } else {
    handlers.onTerminal()
  }
}
