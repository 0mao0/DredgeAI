<template>
  <div class="dredge-mech page-wrap">
    <PageHeader title="疏浚机理" description="耙吸挖泥船实时操耙：地质 + 船舶输入 → 机理项 → 合成产量 → T+5 s 操作建议">
      <template #extra>
        <a-segmented v-model:value="view" :options="viewOptions" />
      </template>
    </PageHeader>

    <!-- 视图 A：立体船（空间位置 + 物料流向）；用 v-show 保留 SVG，切换视图不重画 -->
    <ShipScene v-show="view === 'ship'" :live="live" :active="view === 'ship'" @dispatch="onDispatch" />

    <!-- 视图 B：机理因果链 -->
    <MechanismView
      v-if="view === 'mech'"
      :live="live"
      :hist="sim.hist"
      :spark="sim.spark"
      :events="events"
      :coefficients="sim.coefficients"
      @fit="onFit"
      @reset-coef="onResetCoef"
      @dispatch="onDispatch"
      @reopt="onReopt"
      @set-coef="onSetCoef"
    />
  </div>
</template>

<script setup lang="ts">
/* ==========================================================================
   疏浚机理（耙吸挖泥船实时操耙）
   —— 页面唯一持状态：1 Hz 推进仿真 → 算出 DredgeLive 快照 → 两个视图只读展示。
   模型与公式在 @shared/core/dredge（纯函数、无框架依赖），这里只管时间与交互。
   ========================================================================== */
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import ShipScene from './components/ShipScene.vue'
import MechanismView from './components/MechanismView.vue'
import type { DredgeCoefficients } from '@/types'
import {
  A_TRUE,
  B_TRUE,
  buildLive,
  createDredgeSimState,
  fitCalibration,
  recentEvents,
  tickSimulation,
} from '@shared/core/dredge'

/** 仿真状态（唯一实例；1 Hz 就地推进） */
const sim = reactive(createDredgeSimState())
/** 视图：立体船 / 机理因果链 */
const view = ref<'ship' | 'mech'>('ship')
const viewOptions = [
  { label: '立体船视图', value: 'ship' },
  { label: '机理因果链', value: 'mech' },
]
/** 手动重算触发位（「重新寻优」用：不动仿真，只让 live 重算一次） */
const refreshSeq = ref(0)

/** 当前工况快照：所有展示数值都从这里取 */
const live = computed(() => {
  void refreshSeq.value
  return buildLive(sim)
})
/** 最近事件（已倒序） */
const events = computed(() => recentEvents(sim))

let timer: ReturnType<typeof setInterval> | null = null
onMounted(() => {
  timer = setInterval(tickSimulation, 1000, sim)
})
onBeforeUnmount(() => {
  if (timer) clearInterval(timer)
})

/** 下发指令：原型未接控制链路，只记事件（便于现场核对「谁在什么时候下发了什么」） */
function onDispatch(): void {
  sim.events.push({ t: sim.tick, text: '已下发耙头设定（原型演示，未接控制链路）', warn: false })
}
/** 按窗口数据重新标定 a、b */
function onFit(): void {
  const f = fitCalibration(sim.hist.slice(-160))
  if (!f) return
  sim.coefficients.a = Number(f.a.toFixed(3))
  sim.coefficients.b = Number(f.b.toFixed(3))
  sim.events.push({ t: sim.tick, text: `标定完成：a=${sim.coefficients.a} b=${sim.coefficients.b}（R²=${f.r2.toFixed(3)}）`, warn: false })
}
/** 恢复默认 a、b */
function onResetCoef(): void {
  sim.coefficients.a = A_TRUE
  sim.coefficients.b = B_TRUE
}
/** 手工改 a、b */
function onSetCoef(value: DredgeCoefficients): void {
  sim.coefficients.a = value.a
  sim.coefficients.b = value.b
}
/** 重新寻优：立即重算一次寻优结果 */
function onReopt(): void {
  refreshSeq.value += 1
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.page-wrap {
  width: 100%;
  max-width: 1440px;
  margin: 0 auto;
  padding: @page-padding;
}
@media (max-width: 520px) {
  .page-wrap { padding: @spacing-base; }
}
</style>

<!-- 机理视图的公共类（.chain / .plist / .crow / .tbl / .tag…）跨 index 与 MechanismView 两个组件，
     所以放在非 scoped 块里；选择器已全部限制在 .dredge-mech 下，不会外溢到 app 其它页面 -->
<style lang="less">
@import './styles/mechanism.less';
</style>
