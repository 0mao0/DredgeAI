import { describe, expect, it } from 'vitest'
import { splitSubtitleText, SUBTITLE_MAX_CHARS } from '@/utils/speechText'

describe('splitSubtitleText', () => {
  it('日期作为独立完整的一句，不会被拆开', () => {
    const segments = splitSubtitleText(
      '今天是2026年8月25日咱们今天的晨会主要围绕两项核心任务展开，同时重点强调一下安全注意事项。',
    )
    expect(segments[0]).toBe('今天是2026年8月25日')
  })

  it('每个字幕段都不超过单行上限，不依赖省略号', () => {
    const segments = splitSubtitleText(
      '第一，针对临时用电触电风险，所有配电箱必须规范上锁挂牌，钥匙由专人保管，非电工人员严禁私自打开。',
    )
    for (const segment of segments) {
      expect(segment.replace(/\s/g, '').length).toBeLessThanOrEqual(SUBTITLE_MAX_CHARS)
    }
    expect(segments.length).toBeGreaterThan(1)
  })
})
