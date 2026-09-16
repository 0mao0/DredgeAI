import { describe, expect, it } from 'vitest'
import {
  A_TRUE,
  B_TRUE,
  DREDGE_K,
  SOILS,
  ZONE_TITLE,
  buildCandidates,
  buildLive,
  buildTipRows,
  cuttingRate,
  etaCut,
  fitCalibration,
  isFeasible,
  jetFlow,
  jetRate,
  leverChanged,
  pickRecommended,
  production,
  soilIndexOf,
  transportCapacity,
  volumeConcentration,
  createDredgeSimState,
  tickSimulation,
} from '@shared/core/dredge'
import type { DredgeZone } from '@/types'

/** 可复现的伪随机（xorshift32）：仿真里注入它，测试才能稳定 */
function seeded(seed = 20240916): () => number {
  let x = seed >>> 0
  return () => {
    x ^= x << 13
    x >>>= 0
    x ^= x >> 17
    x ^= x << 5
    x >>>= 0
    return x / 4294967296
  }
}

const finite = (x: unknown): boolean => typeof x === 'number' && Number.isFinite(x)

describe('机理模型（疏浚机理）', () => {
  it('公式量纲自洽：切削项随 v/t 线性、冲水项随 p_j 单调', () => {
    const base = { ucs: 62, t: 0.36, v: 0.95, pj: 1.05, Qj: jetFlow(1.05), rpm: 1180, layer: 3.4, w: 48, soil: 0 }
    expect(cuttingRate({ ...base, v: 1.9 })).toBeCloseTo(cuttingRate(base) * 2, 6)
    expect(cuttingRate({ ...base, t: 0.72 })).toBeCloseTo(cuttingRate(base) * 2, 6)
    expect(cuttingRate({ ...base, ucs: 248 })).toBeLessThan(cuttingRate(base))
    expect(jetRate({ ...base, pj: 1.4, Qj: jetFlow(1.4) })).toBeGreaterThan(jetRate(base))
    /* 地质越硬，切削效率 η_cut 越低 */
    expect(etaCut(SOILS[2].ucs)).toBeLessThan(etaCut(SOILS[0].ucs))
  })

  it('合成产量 = a·h_c + b·h_j，浓度由产量反推', () => {
    const s = createDredgeSimState().setting
    const hc = cuttingRate(s)
    const hj = jetRate(s)
    expect(production(s, A_TRUE, B_TRUE)).toBeCloseTo(A_TRUE * hc + B_TRUE * hj, 9)
    const hex = 600
    expect(volumeConcentration(hex, s.rpm)).toBeCloseTo(hex / (DREDGE_K.QmRef * (s.rpm / DREDGE_K.rpmRef)), 9)
  })

  it('换层按 84 s 周期推进', () => {
    expect(soilIndexOf(0)).toBe(0)
    expect(soilIndexOf(41)).toBe(0)
    expect(soilIndexOf(42)).toBe(1)
    expect(soilIndexOf(66)).toBe(2)
    expect(soilIndexOf(84)).toBe(0)
  })

  it('候选动作全部满足约束，且推荐优先取「≤2 杆」的方案', () => {
    const s = { ...createDredgeSimState().setting, v: 1.0, t: 0.34, pj: 1.1, Qj: jetFlow(1.1), rpm: 1200 }
    const { hexNow, list } = buildCandidates(s, A_TRUE, B_TRUE)
    expect(list.length).toBeGreaterThan(0)
    for (const c of list) {
      expect(isFeasible(c.s, c.hex)).toBe(true)
      expect(c.hex).toBeGreaterThanOrEqual(hexNow - 1e-9) // 只保留「保持 / 增大」
      expect(c.moves).toBeGreaterThanOrEqual(0)
      expect(c.moves).toBeLessThanOrEqual(4)
      expect(finite(c.gainPct) && finite(c.cv) && finite(c.cp)).toBe(true)
    }
    const best = pickRecommended(list)
    expect(best).not.toBeNull()
    expect(best!.moves).toBeLessThanOrEqual(2)
    /* 列表按产量降序 */
    for (let i = 1; i < list.length; i++) expect(list[i - 1].hex).toBeGreaterThanOrEqual(list[i].hex)
    /* 顶到边界时不能出现「超限」候选 */
    const tight = { ...s, v: DREDGE_K.vMax, t: DREDGE_K.tMax, pj: DREDGE_K.pjMax, rpm: DREDGE_K.rpmMax }
    for (const c of buildCandidates(tight, A_TRUE, B_TRUE).list) {
      expect(c.s.v).toBeLessThanOrEqual(DREDGE_K.vMax + 1e-9)
      expect(c.s.t).toBeLessThanOrEqual(DREDGE_K.tMax + 1e-9)
      expect(c.s.pj).toBeLessThanOrEqual(DREDGE_K.pjMax + 1e-9)
      expect(c.s.rpm).toBeLessThanOrEqual(DREDGE_K.rpmMax + 1e-9)
      expect(c.hex).toBeLessThanOrEqual(transportCapacity(c.s) + 1e-9)
      expect(c.cp).toBeLessThanOrEqual(DREDGE_K.cpMax + 1e-9)
    }
  })

  it('伪增量：小于半个最小步长不算「变了」', () => {
    expect(leverChanged('t', 0.36, 0.361)).toBe(false)
    expect(leverChanged('t', 0.36, 0.37)).toBe(true)
    expect(leverChanged('rpm', 1200, 1210)).toBe(false)
    expect(leverChanged('rpm', 1200, 1250)).toBe(true)
  })
})

describe('模拟器（1 Hz 推进）', () => {
  it('推进 200 s：数值全部有限、样本窗口封顶、舱满会转抛泥', () => {
    const sim = createDredgeSimState()
    const rnd = seeded()
    let sawDischarge = false
    for (let i = 0; i < 200; i++) {
      tickSimulation(sim, rnd)
      const live = buildLive(sim)
      if (live.discharging) sawDischarge = true
      expect(finite(live.setting.ucs) && finite(live.setting.v) && finite(live.setting.t)).toBe(true)
      expect(finite(live.production.hexMech) && finite(live.production.hexMeas)).toBe(true)
      expect(finite(live.capacity) && finite(live.capacityUsed)).toBe(true)
      expect(live.hopperFrac).toBeGreaterThanOrEqual(0)
      expect(live.hopperFrac).toBeLessThanOrEqual(1)
      expect(live.candidates.length).toBeLessThanOrEqual(3)
    }
    expect(sim.hist.length).toBe(180) // HIST_MAX
    expect(sim.tick).toBe(200)
    expect(sawDischarge).toBe(true) // 舱满 → 抛泥
    expect(sim.spark.v.length).toBeLessThanOrEqual(40) // SPARK_MAX
    /* 换层事件应发生（84 s 一周期） */
    expect(sim.events.some((e) => e.text.includes('层'))).toBe(true)
  })

  it('标定能反解出生成数据用的真值 a / b', () => {
    const sim = createDredgeSimState()
    const rnd = seeded(7)
    for (let i = 0; i < 180; i++) tickSimulation(sim, rnd)
    const fit = fitCalibration(sim.hist.slice(-160))
    expect(fit).not.toBeNull()
    expect(Math.abs(fit!.a - A_TRUE)).toBeLessThan(0.05)
    expect(Math.abs(fit!.b - B_TRUE)).toBeLessThan(0.05)
    expect(fit!.r2).toBeGreaterThan(0.9)
  })

  it('详情卡 6 个区域都有行且无 NaN', () => {
    const sim = createDredgeSimState()
    const rnd = seeded(11)
    for (let i = 0; i < 30; i++) tickSimulation(sim, rnd)
    const live = buildLive(sim)
    for (const zone of Object.keys(ZONE_TITLE) as DredgeZone[]) {
      const rows = buildTipRows(zone, live)
      expect(rows.length).toBeGreaterThan(0)
      for (const [k, v] of rows) {
        expect(k.length).toBeGreaterThan(0)
        expect(/NaN|undefined|Infinity/.test(v)).toBe(false)
      }
    }
  })
})
