<template>
  <!-- 机理视图（因果链）：从「输入」推到「5 s 后该怎么动耙头」 -->
  <div>
    <!-- 因果链：输入 → 机理 → 合成产量 → 操作建议 -->
    <section class="chain">
      <div class="node">
        <div class="k"><span class="swatch" style="background: var(--v-ucs)" />① 地质 + 船舶输入</div>
        <div class="v">{{ paramCount }}<small>项参数</small></div>
        <div class="n">{{ soilName }} · UCS {{ nf(s.ucs) }} kPa</div>
      </div>
      <div class="node pair">
        <div class="k">
          <span class="swatch" style="background: var(--v-cut)" />
          <span class="swatch" style="background: var(--v-jflow)" />
          ② 机理项（两条并列相加）
        </div>
        <div class="pair-b">
          <div class="half">
            <div class="kh">切削项 h<sub>c</sub></div>
            <div class="v">{{ nf(prod.hc) }}<small>m³/h</small></div>
            <div class="n">η_cut {{ etaCut(s.ucs).toFixed(3) }} · 几何切削率</div>
          </div>
          <div class="half">
            <div class="kh">冲水项 h<sub>j</sub></div>
            <div class="v">{{ nf(prod.hj) }}<small>m³/h</small></div>
            <div class="n">s_p {{ suctionPower(s).toFixed(3) }} m³/m³</div>
          </div>
        </div>
      </div>
      <div class="node hex">
        <div class="k">③ 合成产量 h<sub>ex</sub></div>
        <div class="v">{{ nf(prod.hexMech) }}<small>m³/h</small></div>
        <div class="n">a·h<sub>c</sub>+b·h<sub>j</sub> · 泵侧偏差 {{ pumpDeviation.toFixed(1) }}%</div>
      </div>
      <div class="node">
        <div class="k"><span class="swatch" style="background: var(--color-success)" />④ T+5 s 耙头操作</div>
        <div class="v" style="color: var(--color-success)">{{ best ? `+${best.gainPct.toFixed(1)}%` : '—' }}</div>
        <div class="n">{{ best ? `T+5s → ${nf(best.hex)} m³/h` : '当前无可行调整' }}</div>
      </div>
    </section>

    <!-- 三列：输入 / 机理 / 标定 -->
    <section class="grid c3">
      <!-- 输入 -->
      <SectionCard title="实时输入">
        <template #extra>
          <span class="tag brand">{{ soilName }}</span>
        </template>
        <div class="k-note" style="margin: 0 0 4px">地质条件（决定土体可挖性）</div>
        <div class="plist">
          <div v-for="p in geoParams" :key="p.key" class="prow">
            <div class="pl">
              <span class="swatch" :style="{ background: p.v }" />
              <span class="nm">{{ p.label }}</span><span class="fu">{{ p.unit }}</span>
            </div>
            <div class="pv">{{ nf(paramValue(p, live), p.d) }}</div>
            <svg class="spark" viewBox="0 0 62 18" preserveAspectRatio="none">
              <polyline :points="sparkPoints(spark[p.key])" fill="none" :stroke="p.v" stroke-width="1.4" stroke-linejoin="round" />
            </svg>
          </div>
        </div>
        <div class="k-note" style="margin: 12px 0 4px">船舶实时参数（决定机理项大小）</div>
        <div class="plist">
          <div v-for="p in shipParams" :key="p.key" class="prow">
            <div class="pl">
              <span class="swatch" :style="{ background: p.v }" />
              <span class="nm">{{ p.label }}</span><span class="fu">{{ p.unit }}</span>
            </div>
            <div class="pv">{{ nf(paramValue(p, live), p.d) }}</div>
            <svg class="spark" viewBox="0 0 62 18" preserveAspectRatio="none">
              <polyline :points="sparkPoints(spark[p.key])" fill="none" :stroke="p.v" stroke-width="1.4" stroke-linejoin="round" />
            </svg>
          </div>
        </div>
        <div class="k-note" style="margin: 12px 0 4px">机理事件</div>
        <div>
          <div v-for="(e, i) in events" :key="i" class="logline">
            <time>T{{ e.t }}s</time><span :class="{ warn: e.warn }">{{ e.text }}</span>
          </div>
          <div v-if="!events.length" class="logline">暂无事件</div>
        </div>
      </SectionCard>

      <!-- 机理分解 -->
      <SectionCard title="机理分解">
        <template #extra>
          <span class="tag brand">同色 = 同一物理量</span>
        </template>
        <div class="mb6">
          ② 切削项 h<sub>c</sub>
          <span class="k-note inline">耙齿切削机理 · 原状土几何切削率</span>
        </div>
        <div class="formula">
          h<sub>c</sub> <span class="op">=</span> W <span class="op">·</span> <span class="v-cut">t</span>
          <span class="op">·</span> <span class="v-speed">v</span> <span class="op">·</span> 3600
          <span class="op">·</span> η<sub>cut</sub>
          <span class="op">,</span> &nbsp; η<sub>cut</sub> <span class="op">=</span> 1 / (1 +
          <span class="v-ucs">UCS</span> / UCS<sub>ref</sub>)
        </div>
        <div class="subst">
          = <span class="op">{{ K.W }}</span> × <span class="v-cut">{{ s.t.toFixed(2) }}</span> ×
          <span class="v-speed">{{ (s.v * K.kn).toFixed(4) }}</span> ×
          <span class="op">{{ K.headW }}</span> ×
          <span class="v-ucs">{{ etaCut(s.ucs).toFixed(3) }}</span> =
          <span class="res">{{ nf(prod.hc) }}</span> m³/h
        </div>

        <div class="mb6 mt16">
          ③ 冲水项 h<sub>j</sub>
          <span class="k-note inline">高压冲水破土机理 · 射流掺混</span>
        </div>
        <div class="formula">
          h<sub>j</sub> <span class="op">=</span> <span class="v-jflow">Q<sub>j</sub></span>
          <span class="op">·</span> s<sub>p</sub>
          <span class="op">,</span> &nbsp; s<sub>p</sub> <span class="op">=</span> k<sub>j</sub>
          <span class="op">·</span> √(<span class="v-jetp">p<sub>j</sub></span> / p<sub>ref</sub>) / (1 +
          <span class="v-ucs">UCS</span> / UCS′<sub>ref</sub>)
        </div>
        <div class="subst">
          = <span class="v-jflow">{{ nf(s.Qj) }}</span> ×
          <span class="op">{{ suctionPower(s).toFixed(3) }}</span> =
          <span class="res">{{ nf(prod.hj) }}</span> m³/h
        </div>

        <div class="mb6 mt16">④ 合成产量 h<sub>ex</sub></div>
        <div class="subst" style="margin-top: 0">
          h<sub>ex</sub> = <span class="op">{{ coef.a.toFixed(3) }}</span> ×
          <span class="v-cut">{{ nf(prod.hc) }}</span> +
          <span class="op">{{ coef.b.toFixed(3) }}</span> ×
          <span class="v-jflow">{{ nf(prod.hj) }}</span> =
          <span class="res">{{ nf(prod.hexMech) }}</span> m³/h
        </div>
        <div class="share">
          <i class="s-cut" :style="{ width: `${shareCut}%` }" />
          <i class="s-jet" :style="{ width: `${100 - shareCut}%` }" />
        </div>
        <div class="share-legend">
          <span>切削贡献 <b>{{ nf(prod.aHc) }}</b> m³/h（<b>{{ shareCut.toFixed(0) }}%</b>）</span>
          <span>冲水贡献 <b>{{ nf(prod.bHj) }}</b> m³/h（<b>{{ (100 - shareCut).toFixed(0) }}%</b>）</span>
        </div>
        <div class="k-note">
          泵侧核对：Q<sub>m</sub>·C<sub>v</sub> =
          <span class="v-pump">{{ nf(live.slurryFlow) }}</span> ×
          {{ (prod.cv * 100).toFixed(1) }}% = <b>{{ nf(prod.hexMeas) }}</b> m³/h，与机理侧偏差
          <b>{{ pumpDeviation.toFixed(1) }}%</b>；泵输土能力余量
          <b>{{ nf(live.capacity - prod.hexMech) }}</b> m³/h（已用
          {{ live.capacityUsed.toFixed(0) }}%）。
        </div>
      </SectionCard>

      <!-- 标定 a、b -->
      <SectionCard title="标定 a、b">
        <template #extra>
          <span>用实测产量反求机理权重</span>
        </template>
        <div class="kpis">
          <div class="kpi">
            <div class="k">拟合优度 R²</div>
            <div class="v">{{ fit ? fit.r2.toFixed(3) : '—' }}</div>
          </div>
          <div class="kpi">
            <div class="k">RMSE</div>
            <div class="v">{{ fit ? fit.rmse.toFixed(1) : '—' }}<small>m³/h</small></div>
          </div>
          <div class="kpi">
            <div class="k">标定样本</div>
            <div class="v">{{ fit ? fit.n : '—' }}<small>秒</small></div>
          </div>
        </div>
        <div class="formula formula-sm mt10">
          h<sub>ex</sub> <span class="op">=</span> a <span class="op">·</span> h<sub>c</sub>
          <span class="op">+</span> b <span class="op">·</span> h<sub>j</sub>
        </div>
        <div class="plist mt10">
          <div class="prow" style="grid-template-columns: 1fr 84px 78px">
            <div class="pl">
              <span class="swatch" style="background: var(--v-cut)" />
              <span class="nm">a · 切削项标定系数</span>
            </div>
            <div class="pv">
              <a-input-number v-model:value="aInput" size="small" :step="0.01" :min="0" :max="2" style="width: 100%" />
            </div>
            <div class="pv ci">{{ fit ? `±${(1.96 * fit.seA).toFixed(2)}` : '—' }}</div>
          </div>
          <div class="prow" style="grid-template-columns: 1fr 84px 78px">
            <div class="pl">
              <span class="swatch" style="background: var(--v-jflow)" />
              <span class="nm">b · 冲水项标定系数</span>
            </div>
            <div class="pv">
              <a-input-number v-model:value="bInput" size="small" :step="0.01" :min="0" :max="2" style="width: 100%" />
            </div>
            <div class="pv ci">{{ fit ? `±${(1.96 * fit.seB).toFixed(2)}` : '—' }}</div>
          </div>
        </div>
        <div class="rowbtns">
          <AppButton variant="primary" size="sm" @click="emit('fit')">按窗口数据重新标定</AppButton>
          <AppButton size="sm" @click="emit('resetCoef')">恢复默认</AppButton>
        </div>
        <div class="k-note">
          a、b 表示两条机理的「入舱折算」权重：理论上限产量 → 实际入舱产量的效率。当前泵侧余量
          {{ (100 - live.capacityUsed).toFixed(0) }}%。
        </div>
        <!-- 标定散点：机理预测 vs 泵侧实测 -->
        <svg class="scatter" viewBox="0 0 320 200" style="width: 100%; height: auto; display: block">
          <line
            :x1="sc.px(sc.lo)" :y1="sc.py(sc.lo)" :x2="sc.px(sc.hi)" :y2="sc.py(sc.hi)"
            stroke="var(--color-text-tertiary)" stroke-width="1" stroke-dasharray="4 4"
          />
          <line :x1="64" :y1="sc.py(sc.lo)" :x2="310" :y2="sc.py(sc.lo)" stroke="var(--color-border)" stroke-width="1" />
          <line :x1="64" :y1="10" :x2="64" :y2="sc.py(sc.lo)" stroke="var(--color-border)" stroke-width="1" />
          <circle
            v-for="(d, i) in sc.dots" :key="i"
            :cx="d.x" :cy="d.y" r="2" fill="var(--color-brand)" :opacity="d.opacity"
          />
          <text x="58" :y="sc.py(sc.lo) + 4" text-anchor="end" class="chart-num" fill="var(--color-text-tertiary)">{{ nf(sc.lo) }}</text>
          <text x="58" :y="sc.py(sc.hi) + 10" text-anchor="end" class="chart-num" fill="var(--color-text-tertiary)">{{ nf(sc.hi) }}</text>
          <text :x="(sc.px(sc.lo) + sc.px(sc.hi)) / 2" y="194" text-anchor="middle" class="chart-num" fill="var(--color-text-tertiary)">机理预测 m³/h</text>
          <text
            x="12" :y="(sc.py(sc.lo) + sc.py(sc.hi)) / 2" class="chart-num"
            fill="var(--color-text-tertiary)" text-anchor="middle"
            :transform="`rotate(-90 12 ${(sc.py(sc.lo) + sc.py(sc.hi)) / 2})`"
          >实测 m³/h</text>
        </svg>
      </SectionCard>
    </section>

    <!-- 两列：贡献分解 + 5s 建议 -->
    <section class="grid c2">
      <SectionCard title="贡献分解">
        <template #extra>
          <span>最近 90 s · 两条机理各自的贡献</span>
        </template>
        <svg class="stack" viewBox="0 0 640 220" style="width: 100%; height: auto; display: block">
          <g v-for="(g, i) in st.grid" :key="i">
            <line :x1="70" :y1="g.y" :x2="626" :y2="g.y" stroke="var(--color-divider)" stroke-width="1" />
            <text x="64" :y="g.y + 3" text-anchor="end" class="chart-axis" fill="var(--color-text-tertiary)">{{ nf(g.v) }}</text>
          </g>
          <path v-if="st.cutArea" :d="st.cutArea" fill="var(--v-cut)" opacity=".55" />
          <path v-if="st.jetArea" :d="st.jetArea" fill="var(--v-jflow)" opacity=".55" />
          <path v-if="st.measLine" :d="st.measLine" fill="none" stroke="var(--color-text-primary)" stroke-width="1.6" />
          <text x="70" y="214" class="chart-axis" fill="var(--color-text-tertiary)">−90 s</text>
          <text x="626" y="214" text-anchor="end" class="chart-axis" fill="var(--color-text-tertiary)">现在</text>
        </svg>
        <div class="horizon">
          T+5 s 窗口
          <span class="ticks">
            <i v-for="i in 6" :key="i" :class="i === 1 ? 'now' : 'on'" />
          </span>
        </div>
      </SectionCard>

      <SectionCard title="T+5 s 耙头操作建议">
        <template #extra>
          <span class="tag" :class="adviceLevel === 'ok' ? 'ok' : 'warn'">{{ adviceLevelText }}</span>
        </template>
        <div class="verdict">
          <template v-if="best">
            T+5 s：{{ moveText }}。预计产量
            <b>{{ nf(prod.hexMech) }} → <span class="big">{{ nf(best.hex) }}</span> m³/h</b>
            （<b>+{{ best.gainPct.toFixed(1) }}%</b>，+{{ nf(best.hex - prod.hexMech) }} m³/h）；
            体积浓度 {{ (prod.cv * 100).toFixed(1) }}% → {{ (best.cv * 100).toFixed(1) }}%（上限
            {{ (K.cvMax * 100).toFixed(0) }}%），触底压力 {{ nf(live.bottomPressure) }} →
            {{ nf(best.cp) }} kPa（上限 {{ K.cpMax }}）。
            <br>按此设定持续 1 班（8 h）约多产
            <b>{{ nf((best.hex - prod.hexMech) * 8) }} m³</b>。
          </template>
          <template v-else>当前无可行调整（已触边界）。</template>
        </div>

        <div class="plist mt10">
          <div v-for="L in LEVERS" :key="L.k" class="prow" style="grid-template-columns: 1fr 110px 62px">
            <div class="pl">
              <span class="swatch" :style="{ background: L.v }" />
              <span class="nm">{{ L.nm }}</span><span class="fu">{{ L.u }}</span>
            </div>
            <div class="pv">
              {{ formatLever(L, s[L.k]) }}
              <span :style="{ color: leverUp(L) ? 'var(--color-success)' : 'var(--color-text-tertiary)' }">
                {{ leverUp(L) ? '→' : '=' }}
              </span>
              <b :style="{ color: leverUp(L) ? 'var(--color-success)' : 'var(--color-text-primary)' }">
                {{ formatLever(L, leverNext(L)) }}
              </b>
            </div>
            <svg class="spark" viewBox="0 0 62 18" preserveAspectRatio="none">
              <polyline :points="sparkPoints(spark[L.k])" fill="none" :stroke="L.v" stroke-width="1.4" stroke-linejoin="round" />
            </svg>
          </div>
        </div>

        <div class="mt10">
          <div v-for="c in constraints" :key="c.label" class="crow">
            <span>{{ c.label }}</span>
            <span class="bar"><i :class="levelClass(c.pct)" :style="{ width: `${c.width}%`, background: c.tone }" /></span>
            <span class="cv"><b>{{ c.now }}</b> / {{ c.max }}</span>
          </div>
        </div>

        <div class="tblwrap mt10">
          <table class="tbl">
            <thead>
              <tr>
                <th>方案</th>
                <th class="num">v (kn)</th>
                <th class="num">t (m)</th>
                <th class="num">p_j (MPa)</th>
                <th class="num">预测 h_ex</th>
                <th class="num">增益</th>
                <th class="num">浓度</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(c, i) in live.candidates" :key="i" :class="{ rec: i === 0 }">
                <td>
                  {{ i === 0 ? '★ 推荐' : `方案 ${i + 1}` }}
                  <span class="muted">{{ c.moves }} 杆</span>
                </td>
                <td class="num">{{ c.s.v.toFixed(2) }}</td>
                <td class="num">{{ c.s.t.toFixed(2) }}</td>
                <td class="num">{{ c.s.pj.toFixed(2) }}</td>
                <td class="num"><b>{{ nf(c.hex) }}</b></td>
                <td class="num gain">+{{ c.gainPct.toFixed(1) }}%</td>
                <td class="num">{{ (c.cv * 100).toFixed(1) }}%</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="multiNote" class="k-note">{{ multiNote }}</div>
        <div class="k-note" style="margin-top: 12px">
          增量归因：a·Δh<sub>c</sub> = <b>{{ nf(dCut) }}</b> m³/h（{{ attribCut }}%），b·Δh<sub>j</sub> =
          <b>{{ nf(dJet) }}</b> m³/h（{{ attribJet }}%） —— 说明这一档该怎么使劲，取决于 a、b 的相对大小。
        </div>

        <div class="rowbtns">
          <AppButton variant="primary" size="sm" :disabled="!best" @click="emit('dispatch')">下发到耙头控制</AppButton>
          <AppButton size="sm" @click="emit('reopt')">
            <template #icon><ReloadOutlined /></template>
            重新寻优
          </AppButton>
        </div>
      </SectionCard>
    </section>
  </div>
</template>

<script setup lang="ts">
/* ==========================================================================
   疏浚机理 · 机理视图（视图 B）：因果链 + 参数 + 机理分解 + 标定 + 5s 建议
   所有数值都来自 props.live / hist / spark（页面每 tick 刷新一次），本组件只做展示。
   ========================================================================== */
import { computed, ref, watch } from 'vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { DredgeCoefficients, DredgeEvent, DredgeLever, DredgeLive, DredgeSample, DredgeSparkKey } from '@/types'
import {
  DREDGE_K,
  LEVERS,
  PARAM_DEFS,
  SOILS,
  etaCut,
  fitCalibration,
  formatLever,
  nf,
  paramValue,
  suctionPower,
} from '@shared/core/dredge'

const props = defineProps<{
  /** 当前工况快照 */
  live: DredgeLive
  /** 逐秒样本（标定 / 曲线用） */
  hist: DredgeSample[]
  /** 每项参数的最近采样（迷你趋势线） */
  spark: Record<DredgeSparkKey, number[]>
  /** 最近事件（已倒序） */
  events: DredgeEvent[]
  /** 页面持有的 a、b（输入框双向同步用） */
  coefficients: DredgeCoefficients
}>()

const emit = defineEmits<{
  /** 按窗口数据重新标定 */
  fit: []
  /** 恢复默认 a、b */
  resetCoef: []
  /** 下发到耙头控制 */
  dispatch: []
  /** 重新寻优（重算一次并刷新页面） */
  reopt: []
  /** 手工改 a、b */
  setCoef: [value: DredgeCoefficients]
}>()

const K = DREDGE_K
const s = computed(() => props.live.setting)
const prod = computed(() => props.live.production)
const best = computed(() => props.live.best)
const coef = computed(() => props.live.coefficients)
const soilName = computed(() => SOILS[s.value.soil].name)
const geoParams = computed(() => PARAM_DEFS.filter((p) => p.geo))
const shipParams = computed(() => PARAM_DEFS.filter((p) => !p.geo))
const paramCount = PARAM_DEFS.length

/** 机理侧与泵侧的相对偏差（%） */
const pumpDeviation = computed(() => {
  const rec = prod.value
  return rec.hexMeas ? (100 * (rec.hexMech - rec.hexMeas)) / rec.hexMeas : 0
})
const shareCut = computed(() =>
  prod.value.hexMech ? (prod.value.aHc / prod.value.hexMech) * 100 : 0)

/* ---------- a、b 输入框：跟页面同步，改动回报给页面 ---------- */
const aInput = ref(props.coefficients.a)
const bInput = ref(props.coefficients.b)
watch(() => props.coefficients, (c) => {
  aInput.value = c.a
  bInput.value = c.b
})
watch([aInput, bInput], () => {
  const a = Number(aInput.value)
  const b = Number(bInput.value)
  if (!Number.isFinite(a) || !Number.isFinite(b)) return
  emit('setCoef', { a, b })
})

/* ---------- 迷你趋势线 ---------- */
function sparkPoints(arr: number[] | undefined): string {
  if (!arr || arr.length < 2) return ''
  const w = 62
  const h = 18
  const lo = Math.min(...arr)
  const hi = Math.max(...arr)
  const span = hi - lo || 1
  return arr
    .map((v, i) => `${((i / (arr.length - 1)) * (w - 2) + 1).toFixed(1)},${(h - 2 - ((v - lo) / span) * (h - 4)).toFixed(1)}`)
    .join(' ')
}

/* ---------- 标定：无截距二维最小二乘（机理预测 vs 泵侧实测） ---------- */
const fit = computed(() => fitCalibration(props.hist.slice(-160)))
/** 标定散点：机理预测（x）vs 泵侧实测（y），附 1:1 虚线 */
const sc = computed(() => {
  const rows = props.hist.slice(-160)
  const empty = { lo: 0, hi: 1, px: () => 0, py: () => 0, dots: [] as Array<{ x: number, y: number, opacity: number }> }
  if (!fit.value || rows.length < 8) return empty
  const pred = rows.map((r) => props.coefficients.a * r.hc + props.coefficients.b * r.hj)
  const xs = pred.concat(rows.map((r) => r.hexMeas))
  const lo = Math.min(...xs) * 0.94
  const hi = Math.max(...xs) * 1.04
  const px = (v: number): number => 64 + ((v - lo) / (hi - lo)) * (320 - 64 - 10)
  const py = (v: number): number => 200 - 24 - ((v - lo) / (hi - lo)) * (200 - 24 - 10)
  const dots = rows.map((r, i) => ({
    x: +px(pred[i]).toFixed(1),
    y: +py(r.hexMeas).toFixed(1),
    opacity: i > rows.length - 20 ? 0.85 : 0.35,
  }))
  return { lo, hi, px, py, dots }
})

/* ---------- 堆叠贡献图（最近 90 s） ---------- */
const st = computed(() => {
  const rows = props.hist.slice(-90)
  if (rows.length < 3) return { grid: [] as Array<{ y: number, v: number }>, cutArea: '', jetArea: '', measLine: '' }
  const W = 640
  const H = 220
  const padL = 70
  const padR = 14
  const padT = 12
  const padB = 28
  const totals = rows.map((r) => r.aHc + r.bHj)
  const hi = Math.max(Math.max(...totals), Math.max(...rows.map((r) => r.hexMeas))) * 1.1
  const X = (i: number): number => padL + (i / (rows.length - 1)) * (W - padL - padR)
  const Y = (v: number): number => H - padB - (v / hi) * (H - padB - padT)
  const base = rows.map((r) => r.aHc)
  const cutArea = `M ${X(0)},${Y(0)} ${
    base.map((v, i) => `L ${X(i).toFixed(1)},${Y(v).toFixed(1)}`).join(' ')
  } L ${X(rows.length - 1)},${Y(0)} Z`
  const jetArea = `M ${X(0)},${Y(base[0]).toFixed(1)} ${
    totals.map((v, i) => `L ${X(i).toFixed(1)},${Y(v).toFixed(1)}`).join(' ')
  }${base.map((_v, i) => `L ${X(rows.length - 1 - i).toFixed(1)},${Y(base[rows.length - 1 - i]).toFixed(1)}`).join(' ')
  } Z`
  const measLine = rows.map((r, i) => `${i ? 'L' : 'M'} ${X(i).toFixed(1)},${Y(r.hexMeas).toFixed(1)}`).join(' ')
  const grid = [0, 0.25, 0.5, 0.75, 1].map((f) => ({ y: +Y(hi * f).toFixed(1), v: hi * f }))
  return { grid, cutArea, jetArea, measLine }
})

/* ---------- 建议 ---------- */
const moveText = computed(() => {
  const now = s.value
  const c = best.value?.s
  if (!c) return '保持当前设定'
  const d: string[] = []
  if (c.v > now.v) d.push(`横移速度 <b>${now.v.toFixed(2)}→${c.v.toFixed(2)} kn</b>`)
  if (c.t > now.t) d.push(`切深 <b>${now.t.toFixed(2)}→${c.t.toFixed(2)} m</b>`)
  if (c.pj > now.pj) d.push(`冲水压力 <b>${now.pj.toFixed(2)}→${c.pj.toFixed(2)} MPa</b>`)
  if (c.rpm > now.rpm) d.push(`泥泵转速 <b>${nf(now.rpm)}→${nf(c.rpm)} rpm</b>`)
  return d.join('、') || '保持当前设定'
})
function leverUp(L: DredgeLever): boolean {
  return !!best.value && best.value.s[L.k] > s.value[L.k] + 1e-9
}
const leverNext = (L: DredgeLever): number => best.value?.s[L.k] ?? s.value[L.k]

const constraints = computed(() => {
  const L = props.live
  const rec = prod.value
  const items: Array<[string, number, number, (v: number) => string, string, string]> = [
    ['触底压力', L.bottomPressure, K.cpMax, (v) => nf(v), 'kPa', 'var(--v-cut)'],
    ['体积浓度', rec.cv * 100, K.cvMax * 100, (v) => v.toFixed(1), '%', 'var(--v-pump)'],
    ['泥浆流量', L.slurryFlow, K.QmMax, (v) => nf(v), 'm³/h', 'var(--v-pump)'],
    ['泵输土能力', rec.hexMech, L.capacity, (v) => nf(v), 'm³/h', 'var(--v-hex)'],
  ]
  return items.map(([label, v, max, fmt, unit, tone]) => {
    const pct = (100 * v) / max
    return {
      label,
      pct,
      width: Math.min(100, pct).toFixed(1),
      now: fmt(v),
      max: `${fmt(max)} ${unit}`,
      tone: pct >= 84 ? '' : tone,
    }
  })
})
function levelClass(pct: number): string {
  return pct >= 94 ? 'danger' : pct >= 84 ? 'warn' : ''
}
/** 用「推荐动作下最紧的那条约束」标状态，而不是看当前点 */
const adviceLevel = computed(() => {
  const b = best.value
  if (!b) return 'warn'
  const util = Math.max(
    (100 * b.cv) / K.cvMax,
    (100 * b.cp) / K.cpMax,
    (100 * b.hex) / props.live.capacity,
  )
  return util >= 84 ? 'warn' : 'ok'
})
const adviceLevelText = computed(() => {
  if (!best.value) return '受限'
  const b = best.value
  const util = Math.max((100 * b.cv) / K.cvMax, (100 * b.cp) / K.cpMax, (100 * b.hex) / props.live.capacity)
  return util >= 94 ? '约束贴边 · 谨慎执行' : util >= 84 ? '约束余量偏紧' : '可行'
})
const multiNote = computed(() => {
  const multi = props.live.candidates.slice(1).find((x) => x.moves > 2)
  return multi
    ? `方案 2 / 3 产量更高，但要同时调整 ${multi.moves} 个量；5 s 内一次动这么多杆，现场既不安全、也难判断是哪一杆起了作用，所以推荐列优先生效。`
    : ''
})
const dCut = computed(() => (best.value ? props.coefficients.a * best.value.dHc : 0))
const dJet = computed(() => (best.value ? props.coefficients.b * best.value.dHj : 0))
const attribCut = computed(() => {
  const tot = Math.abs(dCut.value) + Math.abs(dJet.value) || 1
  return ((100 * dCut.value) / tot).toFixed(0)
})
const attribJet = computed(() => {
  const tot = Math.abs(dCut.value) + Math.abs(dJet.value) || 1
  return ((100 * dJet.value) / tot).toFixed(0)
})
</script>

<style scoped lang="less">
/* 视图自身的少量排版（其余公共类在 styles/mechanism.less，已加 .dredge-mech 前缀） */
.mb6 { font-size: var(--font-size-sm); font-weight: 600; margin-bottom: 6px; color: var(--color-text-primary); }
.mt10 { margin-top: 10px; }
.mt16 { margin-top: 16px; }
.inline { display: inline; margin-left: 8px; }
.ci { color: var(--color-text-tertiary); font-size: var(--font-size-xs); }
.muted { color: var(--color-text-tertiary); }
.rowbtns { display: flex; gap: var(--spacing-sm); margin: 10px 0 4px; flex-wrap: wrap; }
</style>
