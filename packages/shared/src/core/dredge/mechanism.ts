import type {
  DredgeCalibration,
  DredgeCandidate,
  DredgeCandidateSet,
  DredgeCoefficients,
  DredgeConstants,
  DredgeLever,
  DredgeLevel,
  DredgeLive,
  DredgeParamDef,
  DredgeProduction,
  DredgeSample,
  DredgeSetting,
  DredgeZone,
  SoilLayer,
} from '@shared/types'

/* ==========================================================================
   1. 常量与假设（可替换点集中在这里：换船 / 换泵 / 换土即改这一组）
   数值来源与「哪些是假设、哪些是实测」的说明见 docs/prototypes/dredge-mechanism-assumptions.md
   ========================================================================== */
export const DREDGE_K: DredgeConstants = {
  W: 3.2, // 耙头有效宽度 m
  ucsRefC: 250, // 切削强度参考 kPa
  ucsRefJ: 180, // 冲水破土强度参考 kPa
  kJ: 0.55, // 破土比基准 m³(原状)/m³(水) @ 1 MPa
  pRef: 1.0, // 冲水压力基准 MPa
  kn: 0.5144, // kn -> m/s
  jetFlowA: 300,
  jetFlowB: 520, // Q_j = A + B·p_j
  cp0: 30,
  cpK: 130,
  cpMax: 88, // 触底压力 = cp0 + cpK·t，上限 kPa
  cvMax: 0.28, // 体积浓度上限
  QmRef: 2450,
  rpmRef: 1180,
  rpmMax: 1400,
  QmMax: 3200,
  pjMax: 1.6,
  vMax: 1.3,
  tMax: 0.46,
  headW: 3600,
  HORIZON: 5,
  hopperCap: 4500, // 泥舱舱容 m³（待确认：换船即改这里）
  hopperK: 600, // 舱载演示加速：1 个 tick(1 s) 视作 10 min
}

/** 模拟用真值：仅用于生成「实测」样本，页面标定结果应逼近它 */
export const A_TRUE = 0.22
export const B_TRUE = 0.6

export const SOILS: readonly SoilLayer[] = [
  { name: '淤泥质粉质黏土', ucs: 62, layer: 3.4, w: 48 },
  { name: '粉砂夹淤泥', ucs: 84, layer: 2.6, w: 39 },
  { name: '中粗砂', ucs: 118, layer: 1.8, w: 28 },
]

/** 4 根可调杆（顺序 = 页面上从上到下） */
export const LEVERS: readonly DredgeLever[] = [
  { k: 't', nm: '切削深度 t', u: 'm', d: 2, v: 'var(--v-cut)' },
  { k: 'v', nm: '横移速度 v', u: 'kn', d: 2, v: 'var(--v-speed)' },
  { k: 'pj', nm: '冲水压力 p_j', u: 'MPa', d: 2, v: 'var(--v-jetp)' },
  { k: 'rpm', nm: '泥泵转速', u: 'rpm', d: 0, v: 'var(--v-pump)' },
]

/** 输入参数看板：geo 组 = 地质量（不可调），其余 = 船上可调/派生量 */
export const PARAM_DEFS: readonly DredgeParamDef[] = [
  { key: 'ucs', label: '地质强度 UCS', unit: 'kPa', v: 'var(--v-ucs)', geo: true, d: 0 },
  { key: 'layer', label: '土层厚度', unit: 'm', v: 'var(--v-ucs)', geo: true, d: 1 },
  { key: 'w', label: '含水率', unit: '%', v: 'var(--v-ucs)', geo: true, d: 0 },
  { key: 'v', label: '横移速度 v', unit: 'kn', v: 'var(--v-speed)', geo: false, d: 2 },
  { key: 't', label: '切削深度 t', unit: 'm', v: 'var(--v-cut)', geo: false, d: 2 },
  { key: 'cp', label: '触底压力', unit: 'kPa', v: 'var(--v-cut)', geo: false, d: 0 },
  { key: 'pj', label: '冲水压力 p_j', unit: 'MPa', v: 'var(--v-jetp)', geo: false, d: 2 },
  { key: 'Qj', label: '冲水流量 Q_j', unit: 'm³/h', v: 'var(--v-jflow)', geo: false, d: 0 },
  { key: 'rpm', label: '泥泵转速', unit: 'rpm', v: 'var(--v-pump)', geo: false, d: 0 },
  { key: 'Qm', label: '泥浆流量 Q_m', unit: 'm³/h', v: 'var(--v-pump)', geo: false, d: 0 },
  { key: 'cv', label: '体积浓度', unit: '%', v: 'var(--v-pump)', geo: false, d: 1 },
]

export const ZONE_TITLE: Record<DredgeZone, string> = {
  prod: '合成产量 h_ex',
  hopper: '泥舱',
  pump: '泥泵 · 输移',
  head: '耙头 · 可调杆',
  soil: '地质 · 前方土体',
  hull: '艏部 · 横移定位',
}

export const ZONE_COLOR: Record<DredgeZone, string> = {
  prod: 'var(--v-hex)',
  hopper: 'var(--v-hex)',
  pump: 'var(--v-pump)',
  head: 'var(--color-brand)',
  soil: 'var(--v-ucs)',
  hull: 'var(--v-speed)',
}

/* ==========================================================================
   2. 机理公式（纯函数：替换这一组即可换模型，页面不动）
   ========================================================================== */
/** 切削效率：地质越硬，同样切深切成率越低 */
export const etaCut = (ucs: number): number => 1 / (1 + ucs / DREDGE_K.ucsRefC)

/** 周期性换层（演示用：42 s 一层，66 s 后进入砂层） */
export const soilIndexOf = (t: number): number => (t % 84 < 42 ? 0 : t % 84 < 66 ? 1 : 2)

/** 破土比 s_p：冲水压力越高破土越多，地质越硬越少 */
export function suctionPower(s: Pick<DredgeSetting, 'pj' | 'ucs'>): number {
  return (DREDGE_K.kJ * Math.sqrt(s.pj / DREDGE_K.pRef)) / (1 + s.ucs / DREDGE_K.ucsRefJ)
}

/** 几何切削率 h_c：宽度 × 切深 × 对地速度 × 时间 × 切削效率 */
export function cuttingRate(s: DredgeSetting): number {
  return DREDGE_K.W * s.t * (s.v * DREDGE_K.kn) * DREDGE_K.headW * etaCut(s.ucs)
}

/** 冲水破土率 h_j：冲水流量 × 破土比 */
export function jetRate(s: Pick<DredgeSetting, 'Qj'> & Pick<DredgeSetting, 'pj' | 'ucs'>): number {
  return s.Qj * suctionPower(s)
}

/** 冲水流量 Q_j = A + B·p_j */
export const jetFlow = (pj: number): number => DREDGE_K.jetFlowA + DREDGE_K.jetFlowB * pj

/** 泥浆流量 Q_m 与转速成正比 */
export const slurryFlow = (rpm: number): number => DREDGE_K.QmRef * (rpm / DREDGE_K.rpmRef)

/** 触底压力随切深线性上升 */
export const bottomPressure = (s: Pick<DredgeSetting, 't'>): number => DREDGE_K.cp0 + DREDGE_K.cpK * s.t

/** 泵侧输土能力：min(泵流量×浓度上限, 泵流量上限×浓度上限) */
export function transportCapacity(s: Pick<DredgeSetting, 'rpm'>): number {
  return Math.min(slurryFlow(s.rpm) * DREDGE_K.cvMax, DREDGE_K.QmMax * DREDGE_K.cvMax)
}

/** 机理侧合成产量 */
export function production(s: DredgeSetting, a: number, b: number): number {
  return a * cuttingRate(s) + b * jetRate(s)
}

/** 由产量反推体积浓度 */
export const volumeConcentration = (hex: number, rpm: number): number => hex / slurryFlow(rpm)

/** 当前工作点是否满足全部约束 */
export function isFeasible(s: DredgeSetting, hex: number): boolean {
  return (
    s.v <= DREDGE_K.vMax && s.t <= DREDGE_K.tMax && s.pj <= DREDGE_K.pjMax && s.rpm <= DREDGE_K.rpmMax
    && bottomPressure(s) <= DREDGE_K.cpMax && hex <= transportCapacity(s)
  )
}

/** 组装完整产量分解（机理侧 + 泵侧对照） */
export function buildProduction(
  s: DredgeSetting,
  coefficients: DredgeCoefficients,
  hexMeas: number,
): DredgeProduction {
  const hc = cuttingRate(s)
  const hj = jetRate(s)
  return {
    hc,
    hj,
    hexMech: production(s, coefficients.a, coefficients.b),
    aHc: coefficients.a * hc,
    bHj: coefficients.b * hj,
    hexMeas,
    cv: volumeConcentration(hexMeas, s.rpm),
  }
}

/* ==========================================================================
   3. 标定：无截距二维最小二乘 hex = a·hc + b·hj（含标准误与 R²）
   ========================================================================== */
export function fitCalibration(rows: readonly DredgeSample[]): DredgeCalibration | null {
  let A = 0; let B = 0; let C = 0; let D = 0; let E = 0
  for (const r of rows) {
    A += r.hc * r.hc
    B += r.hc * r.hj
    C += r.hj * r.hj
    D += r.hc * r.hexMeas
    E += r.hj * r.hexMeas
  }
  const det = A * C - B * B
  if (Math.abs(det) < 1e-9 || rows.length < 8) return null

  const a = (D * C - E * B) / det
  const b = (A * E - B * D) / det
  const n = rows.length
  let ssRes = 0
  let mean = 0
  for (const r of rows) {
    const p = a * r.hc + b * r.hj
    ssRes += (r.hexMeas - p) ** 2
    mean += r.hexMeas
  }
  mean /= n
  let ssTot = 0
  for (const r of rows) ssTot += (r.hexMeas - mean) ** 2
  const sigma2 = ssRes / Math.max(1, n - 2)
  return {
    a,
    b,
    seA: Math.sqrt(Math.max(0, (sigma2 * C) / det)),
    seB: Math.sqrt(Math.max(0, (sigma2 * A) / det)),
    r2: ssTot > 0 ? 1 - ssRes / ssTot : 0,
    rmse: Math.sqrt(ssRes / n),
    n,
  }
}

/* ==========================================================================
   4. 寻优：在候选耙头动作上重算机理项 → 取满足约束的最大产量
   ========================================================================== */
/** 候选步长：都取「5 s 内单杆可执行」的小幅度微调 */
const STEPS = {
  v: [0, 0.05, 0.1],
  t: [0, 0.01, 0.02],
  pj: [0, 0.05, 0.1, 0.15],
  rpm: [0, 50, 100],
}

export function buildCandidates(
  s: DredgeSetting,
  a: number,
  b: number,
): DredgeCandidateSet {
  const hexNow = production(s, a, b)
  const out: DredgeCandidate[] = []
  for (const dv of STEPS.v) {
    for (const dt of STEPS.t) {
      for (const dp of STEPS.pj) {
        for (const dr of STEPS.rpm) {
          const c: DredgeSetting = {
            ...s,
            v: s.v + dv,
            t: Number((s.t + dt).toFixed(3)),
            pj: Number((s.pj + dp).toFixed(3)),
            rpm: s.rpm + dr,
          }
          c.Qj = jetFlow(c.pj)
          const hex = production(c, a, b)
          if (!isFeasible(c, hex)) continue
          out.push({
            s: c,
            hex,
            gainPct: (100 * (hex - hexNow)) / hexNow,
            cv: volumeConcentration(hex, c.rpm),
            cp: bottomPressure(c),
            dHc: cuttingRate(c) - cuttingRate(s),
            dHj: jetRate(c) - jetRate(s),
            moves: [dv > 0, dt > 0, dp > 0, dr > 0].filter(Boolean).length,
          })
        }
      }
    }
  }
  out.sort((x, y) => y.hex - x.hex)
  return { hexNow, list: out }
}

/** 5 s 内不该一次动四个杆：优先在「≤2 个动作」的候选里取最优，其余作为对照 */
export function pickRecommended(list: readonly DredgeCandidate[]): DredgeCandidate | null {
  const lean = list.filter((x) => x.moves <= 2)
  return (lean.length ? lean : list)[0] ?? null
}

/* ==========================================================================
   5. 小工具
   ========================================================================== */
/**
 * 候选动作只含「保持 / 增大」两档；连续漂移会让 dt=0 的候选与当前值差 <1e-3，
 *  按各自最小步长的一半判「保持」，否则会出现「0.37 → 0.37」这种伪增量
 */
const LEVER_EPS: Record<DredgeLever['k'], number> = { v: 0.025, t: 0.005, pj: 0.025, rpm: 25 }

export function leverChanged(k: DredgeLever['k'], cur: number, sug: number): boolean {
  return Math.abs(sug - cur) >= LEVER_EPS[k]
}

/** 千分位格式化（0 位小数时就是整数） */
export function nf(v: number, d = 0): string {
  return Number(v).toLocaleString('zh-CN', { minimumFractionDigits: d, maximumFractionDigits: d })
}

/** 约束占用档位 */
export function utilizationLevel(pct: number): DredgeLevel {
  return pct >= 94 ? 'danger' : pct >= 84 ? 'warn' : 'ok'
}

/** 按杆的定义格式化数值（rpm 取整） */
export function formatLever(L: DredgeLever, x: number): string {
  return L.k === 'rpm' ? nf(x) : x.toFixed(L.d)
}

/** 地层序号 → ①②③ */
export const soilMark = (i: number): string => '①②③'[i] ?? '①'

/** 按土层取泥浆色变量（JS 里给 SVG 填色用） */
export const SOIL_VAR = ['var(--soil-1)', 'var(--soil-2)', 'var(--soil-3)'] as const
export const soilColorVar = (i: number): string => SOIL_VAR[i] ?? SOIL_VAR[0]

/** 输入参数当前值（含派生量 cp / Q_m / C_v） */
export function paramValue(p: DredgeParamDef, live: DredgeLive): number {
  const s = live.setting
  switch (p.key) {
    case 'cp': return live.bottomPressure
    case 'Qm': return live.slurryFlow
    case 'cv': return 100 * live.production.cv
    default: return s[p.key]
  }
}

/* ==========================================================================
   6. 详情卡行（hover / 点按船舶任一区域）
   ========================================================================== */
export function buildTipRows(z: DredgeZone, live: DredgeLive): Array<[string, string]> {
  const { setting: s, production: rec, best } = live
  const gain = best
    ? `${nf(best.hex)} m³/h（${best.gainPct >= 0 ? '+' : ''}${best.gainPct.toFixed(1)} %）`
    : '无可改进方向'
  const pctCap = live.capacityUsed
  switch (z) {
    case 'prod':
      return [
        ['a · h_c 切削贡献', `${nf(rec.aHc)} m³/h`],
        ['b · h_j 冲水贡献', `${nf(rec.bHj)} m³/h`],
        ['合成 h_ex', `${nf(rec.hexMech)} m³/h`],
        ['泵侧实测', `${nf(rec.hexMeas)} m³/h`],
        ['标定 a / b', `${live.coefficients.a.toFixed(3)} / ${live.coefficients.b.toFixed(3)}`],
        ['T+5 s 建议', gain],
      ]
    case 'hopper':
      return [
        ['舱载 / 舱容', `${nf(live.hopper)} / ${nf(DREDGE_K.hopperCap)} m³`],
        ['装载率', `${(live.hopperFrac * 100).toFixed(0)} %`],
        ['体积浓度 Cv', `${(rec.cv * 100).toFixed(1)} / ${(DREDGE_K.cvMax * 100).toFixed(0)} %`],
        ['状态', live.discharging ? '舱满 · 抛泥中' : '装舱中'],
      ]
    case 'pump':
      return [
        ['转速', `${nf(s.rpm)} rpm`],
        ['泥浆流量 Q_m', `${nf(live.slurryFlow)} m³/h`],
        ['输土上限', `${nf(live.capacity)} m³/h`],
        ['输土占用', `${pctCap.toFixed(1)} %`],
        ['余量', `${(100 - pctCap).toFixed(1)} %`],
      ]
    case 'head':
      return [
        ...LEVERS.map((L): [string, string] => [
          `${L.nm}（${L.u}）`,
          `${formatLever(L, s[L.k])}${best && leverChanged(L.k, s[L.k], best.s[L.k]) ? ` → ${formatLever(L, best.s[L.k])}` : ' 保持'}`,
        ]),
        ['触底压力', `${nf(live.bottomPressure)} / ${DREDGE_K.cpMax} kPa`],
        ['T+5 s 增益', gain],
      ]
    case 'soil':
      return [
        ...SOILS.map((x, i): [string, string] => [`${soilMark(i)} ${x.name}`, `${x.ucs} kPa · ${x.layer.toFixed(1)} m`]),
        ['当前含水率', `${s.w.toFixed(0)} %`],
        ['η_c / 破土比 k_j', `${(etaCut(s.ucs) * 100).toFixed(0)} % / ${suctionPower(s).toFixed(3)}`],
      ]
    case 'hull':
      return [
        ['横移速度 v', `${s.v.toFixed(2)} / ${DREDGE_K.vMax} kn`],
        ['相对运动', '海床与水体向后（右）流动'],
        ['前进方向', '向左（艏侧为未挖土体）'],
        ['显示方式', '船体固定，位移由场景滚动表达'],
      ]
    default:
      return []
  }
}
