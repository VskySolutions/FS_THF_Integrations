<template>
  <div class="donut-chart">
    <div v-if="total === 0" class="donut-chart__empty text-grey-6">No Data</div>
    <template v-else>
      <svg viewBox="0 0 42 42" class="donut-chart__svg" role="img" :aria-label="centerLabel || 'Donut chart'">
        <circle class="donut-chart__ring" cx="21" cy="21" r="15.915" fill="transparent" stroke="#eceff1" stroke-width="6" />
        <circle
          v-for="(arc, i) in arcs"
          :key="i"
          cx="21"
          cy="21"
          r="15.915"
          fill="transparent"
          :stroke="arc.color"
          stroke-width="6"
          :stroke-dasharray="`${arc.length} ${100 - arc.length}`"
          :stroke-dashoffset="arc.offset"
        >
          <title>{{ arc.label }}: {{ arc.value }}</title>
        </circle>
        <text v-if="centerLabel" x="21" y="21" text-anchor="middle" dominant-baseline="central" class="donut-chart__center">
          {{ centerLabel }}
        </text>
      </svg>
      <ul class="donut-chart__legend">
        <li v-for="(seg, i) in segments" :key="i" class="donut-chart__legend-item">
          <span class="donut-chart__swatch" :style="{ background: seg.color }" />
          <span class="donut-chart__label">{{ seg.label }}</span>
          <span class="donut-chart__value text-grey-7">{{ seg.value }}</span>
        </li>
      </ul>
    </template>
  </div>
</template>

<script setup>
import { computed } from "vue";

// Dependency-free SVG donut + legend. Renders a "No Data" state when the total is 0.
const props = defineProps({
  // [{ label, value, color }]
  segments: { type: Array, default: () => [] },
  centerLabel: { type: String, default: null }
});

const total = computed(() => props.segments.reduce((s, x) => s + (Number(x.value) || 0), 0));

// Each arc is expressed as a percentage of the 100-unit circumference. Offsets accumulate so segments
// sit end-to-end; the SVG dash starts at 3 o'clock so we offset by 25 to start at 12 o'clock.
const arcs = computed(() => {
  if (total.value === 0) return [];
  let cursor = 0;
  return props.segments.map((seg) => {
    const length = ((Number(seg.value) || 0) / total.value) * 100;
    const offset = 25 - cursor;
    cursor += length;
    return { ...seg, length, offset };
  });
});
</script>

<style scoped>
.donut-chart { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; }
.donut-chart__svg { width: 140px; height: 140px; flex: 0 0 auto; transform: rotate(0deg); }
.donut-chart__center { font-size: 6px; font-weight: 600; fill: #37474f; }
.donut-chart__empty { padding: 32px; text-align: center; width: 100%; }
.donut-chart__legend { list-style: none; margin: 0; padding: 0; flex: 1 1 120px; }
.donut-chart__legend-item { display: flex; align-items: center; gap: 8px; padding: 2px 0; font-size: 15px; }
.donut-chart__swatch { width: 12px; height: 12px; border-radius: 3px; flex: 0 0 auto; }
.donut-chart__value { margin-left: auto; font-weight: 600; }
</style>
