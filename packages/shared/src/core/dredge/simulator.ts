import type {
  DredgeEvent,
  DredgeLive,
  DredgeSample,
  DredgeSetting,
  DredgeSimState,
  DredgeSparkKey,
} from '@shared/types'
import {
  A_TRUE,
  B_TRUE,
  DREDGE_K,
  SOILS,
  buildCandidates,
  buildProduction,
  bottomPressure,
  cuttingRate,
  jetFlow,
  jetRate,
  pickRecommended,
  soilIndexOf,
  slurryFlow,
  transportCapacity,
} from './mechanism'

/** 逐秒样本保留长度 */
export const HIST_MAX = 180
/** 迷你趋势线采样长度 */
export const SPARK_MAX = 40

const SPARK_KEYS: readonly DredgeSparkKey[] = ['ucs', 't', 'v', 'pj', 'Qj', 'rpm', 'Qm', 'cv', 'cp', 'layer', 'w']

/** 初始工况：首屏给半舱，避免空图 */
export function createDredgeSimState(): DredgeSimState {
  const setting: DredgeSetting = {
    ucs: SOILS[0].ucs,
    t: 0.36,
    v: 0.95,
    pj: 1.05,
    Qj: jetFlow(1.05),
    rpm: 1180,
    layer: SOILS[0].layer,
    w: SOILS[0].w,
    soil: 0,
  }
  const spark = {} as Record<DredgeSparkKey, number[]>
  for (const k of SPARK_KEYS) spark[k] = []
  return {
    tick: 0,
    setting,
    coefficients: { a: A_TRUE, b: B_TRUE },
    hist: [],
    spark,
    events: [],
    hopper: 2400,
    dischargeUntil: -1,
    roll: 0,
    scroll: 0,
  }
}

/** 随机游走：把参数收在可观范围内，保证「还有调整余量」可展示 */
function walk(x: number, step: number, lo: number, hi: number, rnd: () => number): number {
  return Math.min(hi, Math.max(lo, x + (rnd() - 0.5) * 2 * step))
}

/**
 * 推进 1 s：地质换层 → 船舶参数漂移 → 生成含噪实测样本 → 舱载累积。
 * 就地修改 state（页面持有唯一实例，Vue 的 reactive 能追踪到）。
 * rnd 可注入，便于测试复现。
 */
export function tickSimulation(state: DredgeSimState, rnd: () => number = Math.random): void {
  const s = state.setting
  state.tick += 1

  /* 周期性换层：让 h_c / h_j 有足够变化，标定才有意义 */
  const soilIdx = soilIndexOf(state.tick)
  if (soilIdx !== s.soil) {
    s.soil = soilIdx
    const soil = SOILS[soilIdx]
    s.ucs = soil.ucs + (rnd() - 0.5) * 6
    s.layer = soil.layer
    s.w = soil.w
    state.events.push({
      t: state.tick,
      text: `进入${soil.name}层（UCS ${s.ucs.toFixed(0)} kPa）`,
      warn: soilIdx === 2,
    })
  }

  /* 船舶参数随机游走 */
  s.v = walk(s.v, 0.035, 0.82, 1.08, rnd)
  s.t = walk(s.t, 0.01, 0.3, 0.4, rnd)
  s.pj = walk(s.pj, 0.04, 0.95, 1.28, rnd)
  s.Qj = jetFlow(s.pj)
  s.rpm = Math.round(walk(s.rpm, 16, 1150, 1280, rnd))
  /* UCS 跟随当前土类（土类名与强度不能自相矛盾），只做小幅波动 */
  const nominal = SOILS[s.soil].ucs
  s.ucs = walk(s.ucs, 2, nominal - 12, nominal + 14, rnd)

  const hc = cuttingRate(s)
  const hj = jetRate(s)
  /* 泵侧「实测」= 真值模型 + ±3% 计量噪声；页面标定应逼近 A_TRUE / B_TRUE */
  const hexMeas = (A_TRUE * hc + B_TRUE * hj) * (1 + (rnd() - 0.5) * 0.06)

  /* 舱载累积（加速演示：1 s 视作 10 min） */
  state.hopper += (hexMeas / 3600) * DREDGE_K.hopperK
  if (state.hopper >= DREDGE_K.hopperCap) {
    state.hopper = 0
    state.dischargeUntil = state.tick + 12
    state.events.push({
      t: state.tick,
      text: `泥舱满 ${DREDGE_K.hopperCap.toLocaleString('zh-CN')} m³ → 停装舱、溢流转排污（演示）`,
      warn: true,
    })
  }

  const sample: DredgeSample = {
    t: state.tick,
    hc,
    hj,
    hexMeas,
    aHc: state.coefficients.a * hc,
    bHj: state.coefficients.b * hj,
  }
  state.hist.push(sample)
  if (state.hist.length > HIST_MAX) state.hist.shift()

  /* 轻微横摇（船体固定显示，位移由世界滚动表达） */
  state.roll = Math.sin(state.tick * 0.42) * 0.32

  /* 迷你趋势线采样 */
  for (const k of SPARK_KEYS) {
    let value: number
    switch (k) {
      case 'Qm': value = slurryFlow(s.rpm); break
      case 'cv': value = 100 * (hexMeas / slurryFlow(s.rpm)); break
      case 'cp': value = bottomPressure(s); break
      default: value = s[k]; break
    }
    state.spark[k].push(value)
    if (state.spark[k].length > SPARK_MAX) state.spark[k].shift()
  }
}

/** 由当前状态算出页面只读的工况快照（含寻优结果） */
export function buildLive(state: DredgeSimState): DredgeLive {
  const s = state.setting
  const last = state.hist[state.hist.length - 1]
  const hexMeas = last ? last.hexMeas : A_TRUE * cuttingRate(s) + B_TRUE * jetRate(s)
  const prod = buildProduction(s, state.coefficients, hexMeas)
  const { list } = buildCandidates(s, state.coefficients.a, state.coefficients.b)
  const best = pickRecommended(list)
  /* 对照方案：推荐 + 次优 2 个（页面表格用） */
  const candidates = best ? [best, ...list.filter((x) => x !== best).slice(0, 2)] : []
  const capacity = transportCapacity(s)
  return {
    tick: state.tick,
    setting: s,
    production: prod,
    best,
    candidates,
    coefficients: { ...state.coefficients },
    capacity,
    bottomPressure: bottomPressure(s),
    slurryFlow: slurryFlow(s.rpm),
    capacityUsed: (100 * prod.hexMech) / capacity,
    hopperFrac: Math.max(0, Math.min(1, state.hopper / DREDGE_K.hopperCap)),
    hopper: state.hopper,
    discharging: state.tick < state.dischargeUntil,
  }
}

/** 事件流（最近 n 条，倒序） */
export function recentEvents(state: DredgeSimState, n = 4): DredgeEvent[] {
  return state.events.slice(-n).reverse()
}
