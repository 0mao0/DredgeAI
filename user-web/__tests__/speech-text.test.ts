import { describe, expect, it } from 'vitest'
import { splitSubtitleText, splitSpeechText, SUBTITLE_MAX_CHARS } from '@/utils/speechText'

describe('splitSpeechText', () => {
  it('按断句拆分，不再合并成 60 字大段', () => {
    const segments = splitSpeechText(
      '各位工友，大家早上好。今天重点抓两件事：一是临时用电专项检查，二是动火作业。请大家配合。',
    )
    expect(segments[0]).toBe('各位工友，大家早上好。')
    expect(segments.some((s) => s.replace(/\s/g, '').length > 30)).toBe(false)
    expect(segments.join('')).toBe('各位工友，大家早上好。今天重点抓两件事：一是临时用电专项检查，二是动火作业。请大家配合。')
  })

  it('首段保持为开场句（与服务端缓存一致），后续断句不被并入首段', () => {
    const segments = splitSpeechText('各位工友，大家早上好。第一，注意安全；第二，按章作业。')
    expect(segments[0]).toBe('各位工友，大家早上好。')
    expect(segments[1]).toBe('第一，注意安全；')
  })

  it('过短碎片并入相邻断句，不留孤零零的碎句', () => {
    const segments = splitSpeechText(
      '各位工友，大家早上好。今天要重点做好现场安全管理和文明施工，一、所有人员必须正确佩戴安全帽；二、禁止吸烟。',
    )
    for (let i = 1; i < segments.length - 1; i++) {
      expect(segments[i]!.replace(/\s/g, '').length).toBeGreaterThanOrEqual(4)
    }
    expect(segments[0]).toBe('各位工友，大家早上好。')
  })
})

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
