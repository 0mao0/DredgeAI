<template>
  <div class="chart-container" :style="{ height }">
    <a-spin v-if="loading" class="chart-spin" />
    <v-chart v-else :option="option" autoresize class="chart" />
  </div>
</template>

<script setup lang="ts">
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart, PieChart } from 'echarts/charts'
import {
  TitleComponent, TooltipComponent, LegendComponent,
  GridComponent, DataZoomComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'

use([
  CanvasRenderer, LineChart, BarChart, PieChart,
  TitleComponent, TooltipComponent, LegendComponent,
  GridComponent, DataZoomComponent,
])

defineProps<{
  option: Record<string, unknown>
  height?: string
  loading?: boolean
}>()
</script>

<style scoped lang="less">
.chart-container {
  width: 100%;
  position: relative;
}
.chart-spin {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}
.chart {
  width: 100%;
  height: 100%;
}
</style>
