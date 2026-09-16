/**
 * 疏浚机理（耙吸挖泥船实时操耙）共享类型。
 * 页面只负责展示，所有数值都由 core/dredge/mechanism.ts 的纯函数算出，
 * 后续若把模型搬到后端，前端可原样替换为接口返回。
 */

/** 机理模型常量（可替换点集中在这里：换船/换泵/换土即改这一组） */
export interface DredgeConstants {
  /** 耙头有效宽度 m */
  W: number
  /** 切削强度参考 kPa */
  ucsRefC: number
  /** 冲水破土强度参考 kPa */
  ucsRefJ: number
  /** 破土比基准 m³(原状)/m³(水) @1MPa */
  kJ: number
  /** 冲水压力基准 MPa */
  pRef: number
  /** kn -> m/s */
  kn: number
  /** 冲水流量 Q_j = jetFlowA + jetFlowB·p_j */
  jetFlowA: number
  jetFlowB: number
  /** 触底压力 = cp0 + cpK·t，上限 cpMax kPa */
  cp0: number
  cpK: number
  cpMax: number
  /** 体积浓度上限 */
  cvMax: number
  /** 泥泵：额定流量与转速 */
  QmRef: number
  rpmRef: number
  /** 泥泵：转速上限与流量上限 */
  rpmMax: number
  QmMax: number
  /** 各可调杆上限 */
  pjMax: number
  vMax: number
  tMax: number
  /** 小时换算 */
  headW: number
  /** 建议时域（秒） */
  HORIZON: number
  /** 泥舱舱容 m³（换船即改这里） */
  hopperCap: number
  /** 舱载演示加速：1 个 tick(1s) 视作 10 min */
  hopperK: number
}

/** 土层（地质强度、层厚、含水率） */
export interface SoilLayer {
  name: string
  /** 无侧限抗压强度 kPa */
  ucs: number
  /** 层厚 m */
  layer: number
  /** 含水率 % */
  w: number
}

/** 当前工作点：3 个地质量 + 4 个可调杆 */
export interface DredgeSetting {
  /** 地质强度 UCS kPa */
  ucs: number
  /** 切削深度 t m */
  t: number
  /** 横移速度 v kn */
  v: number
  /** 冲水压力 p_j MPa */
  pj: number
  /** 冲水流量 Q_j m³/h（由 p_j 派生，随设置一起存） */
  Qj: number
  /** 泥泵转速 rpm */
  rpm: number
  /** 土层厚度 m */
  layer: number
  /** 含水率 % */
  w: number
  /** 当前土层序号 0/1/2 */
  soil: number
}

/** 机理两条支路的瞬时速率 */
export interface DredgeTerms {
  /** 几何切削率 m³/h */
  hc: number
  /** 冲水破土率 m³/h */
  hj: number
}

/** 标定系数（入舱折算权重） */
export interface DredgeCoefficients {
  a: number
  b: number
}

/** 合成产量与分解 */
export interface DredgeProduction extends DredgeTerms {
  /** 机理侧合成产量 a·hc + b·hj */
  hexMech: number
  /** 切削项贡献 a·hc */
  aHc: number
  /** 冲水项贡献 b·hj */
  bHj: number
  /** 泵侧实测（Q_m·C_v） */
  hexMeas: number
  /** 体积浓度（由产量反推） */
  cv: number
}

/** 逐秒样本（标定与曲线用） */
export interface DredgeSample {
  t: number
  hc: number
  hj: number
  hexMeas: number
  aHc: number
  bHj: number
}

/** 二维无截距最小二乘标定结果 */
export interface DredgeCalibration {
  a: number
  b: number
  /** a 的 95% 置信半宽 */
  seA: number
  seB: number
  r2: number
  rmse: number
  n: number
}

/** 一个候选耙头动作 */
export interface DredgeCandidate {
  s: DredgeSetting
  /** 该动作下的机理产量 */
  hex: number
  /** 相对当前点的增益 % */
  gainPct: number
  cv: number
  cp: number
  dHc: number
  dHj: number
  /** 这一档同时动了几个杆 */
  moves: number
}

/** 寻优结果 */
export interface DredgeCandidateSet {
  /** 当前产量 */
  hexNow: number
  /** 可行候选，按产量降序 */
  list: DredgeCandidate[]
}

/** 可调杆定义（4 根） */
export interface DredgeLever {
  k: 't' | 'v' | 'pj' | 'rpm'
  /** 中文名（页面上直接用，避免 v / pj 这类缩写看不懂） */
  nm: string
  /** 单位 */
  u: string
  /** 小数位 */
  d: number
  /** 变量名（对应 CSS 变量 --v-*） */
  v: string
}

/** 参数行定义（输入参数看板用） */
export interface DredgeParamDef {
  key: 'ucs' | 'layer' | 'w' | 'v' | 't' | 'cp' | 'pj' | 'Qj' | 'rpm' | 'Qm' | 'cv'
  label: string
  unit: string
  /** 变量名（对应 CSS 变量 --v-*） */
  v: string
  /** true = 地质量（不可调） */
  geo: boolean
  d: number
}

/** 状态事件（进入土层、舱满、下发指令、标定…） */
export interface DredgeEvent {
  t: number
  text: string
  warn: boolean
}

/** 趋势迷你图用的采样序列 key */
export type DredgeSparkKey = 'ucs' | 't' | 'v' | 'pj' | 'Qj' | 'rpm' | 'Qm' | 'cv' | 'cp' | 'layer' | 'w'

/** 模拟器状态（页面每 1 s 推进一格；纯数据结构，便于测试与换模型） */
export interface DredgeSimState {
  /** 已推进秒数 */
  tick: number
  setting: DredgeSetting
  coefficients: DredgeCoefficients
  /** 逐秒样本（标定 / 曲线用，保留最近 180 s） */
  hist: DredgeSample[]
  /** 每项参数的最近 40 个采样（迷你趋势线） */
  spark: Record<DredgeSparkKey, number[]>
  events: DredgeEvent[]
  /** 舱载 m³ */
  hopper: number
  /** 舱满后转抛泥的截止 tick */
  dischargeUntil: number
  /** 横摇角 deg */
  roll: number
  /** 世界滚动累计秒数（rAF 驱动，速度 ∝ v） */
  scroll: number
}

/** 船舶视图的命中区域 */
export type DredgeZone = 'prod' | 'hopper' | 'pump' | 'head' | 'soil' | 'hull'

/** 约束占用档位：≥94% 贴边、≥84% 偏紧、其余正常 */
export type DredgeLevel = 'ok' | 'warn' | 'danger'

/** 当前工况快照：每个 tick 由模拟器产出一次，页面（含详情卡）只读这份数据 */
export interface DredgeLive {
  /** 已推进的秒数 */
  tick: number
  setting: DredgeSetting
  production: DredgeProduction
  /** 推荐动作；null = 已顶到约束边界 */
  best: DredgeCandidate | null
  /** 对照方案（推荐 + 次优 2 个），建议卡表格直接用 */
  candidates: DredgeCandidate[]
  coefficients: DredgeCoefficients
  /** 泵侧输土能力 m³/h */
  capacity: number
  /** 触底压力 kPa */
  bottomPressure: number
  /** 泥浆流量 Q_m m³/h */
  slurryFlow: number
  /** 泵输土占用 % */
  capacityUsed: number
  /** 舱载装载率 0~1 */
  hopperFrac: number
  /** 舱载 m³ */
  hopper: number
  /** 是否处于舱满抛泥阶段 */
  discharging: boolean
}
