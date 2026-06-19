<template>
  <div class="line-chart">
    <svg :viewBox="`0 0 ${width} ${height}`" class="line-chart__svg" preserveAspectRatio="none" role="img" aria-label="Line chart">
      <line :x1="pad" :y1="height - pad" :x2="width - 2" :y2="height - pad" stroke="#cfd8dc" stroke-width="0.4" />
      <polyline
        v-for="(s, si) in series"
        :key="si"
        :points="pointsFor(s)"
        fill="none"
        :stroke="s.color"
        stroke-width="1"
        stroke-linejoin="round"
        stroke-linecap="round"
      >
        <title>{{ s.name }}</title>
      </polyline>
    </svg>
    <ul class="line-chart__legend">
      <li v-for="(s, i) in series" :key="i" class="line-chart__legend-item">
        <span class="line-chart__swatch" :style="{ background: s.color }" />{{ s.name }}
      </li>
    </ul>
  </div>
</template>

<script setup>
import { computed } from "vue";

// Dependency-free SVG multi-line chart.
const props = defineProps({
  labels: { type: Array, default: () => [] },
  // [{ name, color, values:number[] }]
  series: { type: Array, default: () => [] }
});

const width = 100;
const height = 50;
const pad = 3;

const max = computed(() => {
  let m = 0;
  props.series.forEach((s) => (s.values || []).forEach((v) => { m = Math.max(m, Number(v) || 0); }));
  return m || 1;
});

const count = computed(() => Math.max(props.labels.length, 1));
const stepX = computed(() => (count.value > 1 ? (width - pad - 2) / (count.value - 1) : 0));
const chartH = computed(() => height - pad * 2);

const pointsFor = (s) =>
  (s.values || [])
    .map((v, i) => `${(pad + i * stepX.value).toFixed(2)},${(height - pad - (Number(v) || 0) / max.value * chartH.value).toFixed(2)}`)
    .join(" ");
</script>

<style scoped>
.line-chart__svg { width: 100%; height: 200px; }
.line-chart__legend { list-style: none; display: flex; flex-wrap: wrap; gap: 12px; margin: 8px 0 0; padding: 0; font-size: 14px; }
.line-chart__legend-item { display: flex; align-items: center; gap: 6px; }
.line-chart__swatch { width: 12px; height: 12px; border-radius: 3px; }
</style>
