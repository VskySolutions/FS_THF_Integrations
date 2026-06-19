<template>
  <div class="bar-chart">
    <svg :viewBox="`0 0 ${width} ${height}`" class="bar-chart__svg" preserveAspectRatio="none" role="img" aria-label="Bar chart">
      <line :x1="pad" :y1="height - pad" :x2="width - 4" :y2="height - pad" stroke="#cfd8dc" stroke-width="0.5" />
      <template v-for="(cat, ci) in categories" :key="ci">
        <rect
          v-for="bar in barsFor(ci)"
          :key="bar.key"
          :x="bar.x"
          :y="bar.y"
          :width="bar.w"
          :height="bar.h"
          :fill="bar.color"
          rx="0.5"
        >
          <title>{{ bar.name }} — {{ cat }}: {{ bar.value }}</title>
        </rect>
        <text :x="groupX(ci) + groupWidth / 2" :y="height - 1" text-anchor="middle" class="bar-chart__cat">{{ cat }}</text>
      </template>
    </svg>
    <ul class="bar-chart__legend">
      <li v-for="(s, i) in series" :key="i" class="bar-chart__legend-item">
        <span class="bar-chart__swatch" :style="{ background: s.color }" />{{ s.name }}
      </li>
    </ul>
  </div>
</template>

<script setup>
import { computed } from "vue";

// Dependency-free SVG grouped/stacked bar chart with hover <title> tooltips.
const props = defineProps({
  categories: { type: Array, default: () => [] },
  // [{ name, color, values:number[] }]
  series: { type: Array, default: () => [] },
  stacked: { type: Boolean, default: false }
});

const width = 100;
const height = 60;
const pad = 4;

const max = computed(() => {
  let m = 0;
  props.categories.forEach((_, ci) => {
    if (props.stacked) {
      m = Math.max(m, props.series.reduce((s, ser) => s + (Number(ser.values?.[ci]) || 0), 0));
    } else {
      props.series.forEach((ser) => { m = Math.max(m, Number(ser.values?.[ci]) || 0); });
    }
  });
  return m || 1;
});

const chartW = computed(() => width - pad - 4);
const chartH = computed(() => height - pad - 6);
const groupWidth = computed(() => (props.categories.length ? chartW.value / props.categories.length : 0));
const groupX = (ci) => pad + ci * groupWidth.value;
const scaleY = (v) => (v / max.value) * chartH.value;

const barsFor = (ci) => {
  const gx = groupX(ci);
  const inner = groupWidth.value * 0.7;
  const innerX = gx + (groupWidth.value - inner) / 2;
  const baseY = height - pad;
  if (props.stacked) {
    let acc = 0;
    return props.series.map((ser, si) => {
      const v = Number(ser.values?.[ci]) || 0;
      const h = scaleY(v);
      acc += h;
      return { key: si, x: innerX, w: inner, y: baseY - acc, h, color: ser.color, name: ser.name, value: v };
    });
  }
  const bw = props.series.length ? inner / props.series.length : inner;
  return props.series.map((ser, si) => {
    const v = Number(ser.values?.[ci]) || 0;
    const h = scaleY(v);
    return { key: si, x: innerX + si * bw, w: bw * 0.85, y: baseY - h, h, color: ser.color, name: ser.name, value: v };
  });
};
</script>

<style scoped>
.bar-chart__svg { width: 100%; height: 200px; }
.bar-chart__cat { font-size: 3px; fill: #607d8b; }
.bar-chart__legend { list-style: none; display: flex; flex-wrap: wrap; gap: 12px; margin: 8px 0 0; padding: 0; font-size: 14px; }
.bar-chart__legend-item { display: flex; align-items: center; gap: 6px; }
.bar-chart__swatch { width: 12px; height: 12px; border-radius: 3px; }
</style>
