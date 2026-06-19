<template>
  <div class="gauge-chart">
    <svg viewBox="0 0 120 70" class="gauge-chart__svg" role="img" :aria-label="`${label || 'Gauge'}: ${clamped}%`">
      <path d="M10 60 A50 50 0 0 1 110 60" fill="none" stroke="#eceff1" stroke-width="12" stroke-linecap="round" />
      <path :d="arcPath" fill="none" :stroke="color" stroke-width="12" stroke-linecap="round">
        <title>{{ label }}: {{ clamped }}%</title>
      </path>
      <text x="60" y="52" text-anchor="middle" class="gauge-chart__value" :fill="color">{{ clamped }}%</text>
    </svg>
    <div v-if="label" class="gauge-chart__label text-grey-7">{{ label }}</div>
  </div>
</template>

<script setup>
import { computed } from "vue";

// Dependency-free SVG semicircle gauge. Below `threshold` it renders the warning colour.
const props = defineProps({
  value: { type: Number, default: 0 },
  threshold: { type: Number, default: 90 },
  label: { type: String, default: null }
});

const clamped = computed(() => Math.max(0, Math.min(100, Math.round(Number(props.value) || 0))));
const color = computed(() => (clamped.value < props.threshold ? "#f2994a" : "#21ba45"));

// Sweep a 180° arc (radius 50, centre 60,60) from the left endpoint to the value's angle.
const arcPath = computed(() => {
  const angle = Math.PI * (1 - clamped.value / 100);
  const x = 60 + 50 * Math.cos(angle);
  const y = 60 - 50 * Math.sin(angle);
  const largeArc = clamped.value > 50 ? 1 : 0;
  return `M10 60 A50 50 0 0 ${largeArc} ${x.toFixed(2)} ${y.toFixed(2)}`;
});
</script>

<style scoped>
.gauge-chart { text-align: center; }
.gauge-chart__svg { width: 100%; max-width: 220px; height: auto; }
.gauge-chart__value { font-size: 18px; font-weight: 700; }
.gauge-chart__label { font-size: 15px; margin-top: 4px; }
</style>
