import { describe, expect, it, vi } from 'vitest'
import {
  applyReadTaskStatus,
  isReadPollingStatus,
  READ_POLLING_STATUSES,
} from '@/views/ai-bid/read/readPolling'

function makeHandlers() {
  return {
    onCleared: vi.fn(),
    onParsed: vi.fn(),
    onLeftParsed: vi.fn(),
    onStartPolling: vi.fn(),
    onTerminal: vi.fn(),
  }
}

describe('isReadPollingStatus', () => {
  it('轮询态返回 true', () => {
    for (const status of READ_POLLING_STATUSES) {
      expect(isReadPollingStatus(status)).toBe(true)
    }
  })

  it('终态与空值返回 false', () => {
    expect(isReadPollingStatus('ready')).toBe(false)
    expect(isReadPollingStatus('partial')).toBe(false)
    expect(isReadPollingStatus('failed')).toBe(false)
    expect(isReadPollingStatus(null)).toBe(false)
    expect(isReadPollingStatus(undefined)).toBe(false)
    expect(isReadPollingStatus('')).toBe(false)
  })
})

describe('applyReadTaskStatus', () => {
  it('任务被清空时只触发 onCleared', () => {
    const handlers = makeHandlers()
    applyReadTaskStatus(null, handlers)
    expect(handlers.onCleared).toHaveBeenCalledTimes(1)
    expect(handlers.onParsed).not.toHaveBeenCalled()
    expect(handlers.onLeftParsed).not.toHaveBeenCalled()
    expect(handlers.onStartPolling).not.toHaveBeenCalled()
    expect(handlers.onTerminal).not.toHaveBeenCalled()
  })

  it('parsed：启动看门狗并继续轮询', () => {
    const handlers = makeHandlers()
    applyReadTaskStatus('parsed', handlers)
    expect(handlers.onParsed).toHaveBeenCalledTimes(1)
    expect(handlers.onLeftParsed).not.toHaveBeenCalled()
    expect(handlers.onStartPolling).toHaveBeenCalledTimes(1)
    expect(handlers.onTerminal).not.toHaveBeenCalled()
  })

  it('其余轮询态：不启看门狗，继续轮询', () => {
    for (const status of ['uploading', 'parsing', 'extracting', 'reviewing'] as const) {
      const handlers = makeHandlers()
      applyReadTaskStatus(status, handlers)
      expect(handlers.onLeftParsed).toHaveBeenCalledTimes(1)
      expect(handlers.onStartPolling).toHaveBeenCalledTimes(1)
      expect(handlers.onTerminal).not.toHaveBeenCalled()
    }
  })

  it('回归：终态必须触发 onTerminal（补拉一轮完整数据）', () => {
    // 缺陷现场：轮询在途时任务到达终态，stopPolling() 使 pollGen++，
    // 在途 refreshDetail 后续的 baseline/parsed document 请求因 stale() 被跳过，
    // 页面停留在“未抽取到任何基准库字段”，但后端字段已落库。
    // 修复：终态必须停止轮询后补拉一轮完整数据（onTerminal）。
    for (const status of ['ready', 'partial', 'failed'] as const) {
      const handlers = makeHandlers()
      applyReadTaskStatus(status, handlers)
      expect(handlers.onLeftParsed).toHaveBeenCalledTimes(1)
      expect(handlers.onStartPolling).not.toHaveBeenCalled()
      expect(handlers.onTerminal).toHaveBeenCalledTimes(1)
    }
  })
})
