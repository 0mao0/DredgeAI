/**
 * 估算配音 token 消耗。
 * TODO: 当前为前端经验公式，token 计费应由后端生成配音时返回权威值。
 */
export function estimateDubbingTokenCost(charCount: number): number {
  return Math.ceil(charCount / 1.5) + 50
}
