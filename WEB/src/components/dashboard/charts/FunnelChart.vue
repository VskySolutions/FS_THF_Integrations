<template>
  <div class="funnel-chart">
    <div v-if="!stages.length" class="funnel-chart__empty text-grey-6">No Data</div>
    <div v-for="(row, i) in rows" :key="i" class="funnel-chart__row" :class="{ 'funnel-chart__row--bottleneck': row.bottleneck }">
      <div class="funnel-chart__label">{{ row.label }}</div>
      <div class="funnel-chart__track">
        <div class="funnel-chart__bar" :style="{ width: row.width + '%', background: row.color }">
          <span class="funnel-chart__value">{{ row.value }}</span>
        </div>
      </div>
      <q-icon v-if="row.bottleneck" name="o_warning" color="negative" size="18px" class="funnel-chart__flag">
        <q-tooltip>Largest drop-off</q-tooltip>
      </q-icon>
    </div>
  </div>
</template>

<script setup>
import { computed } from "vue";

// Dependency-free horizontal stepped funnel. When `highlightBottleneck` is set, the stage following
// the largest absolute drop is highlighted.
const props = defineProps({
  // [{ label, value, color? }]
  stages: { type: Array, default: () => [] },
  highlightBottleneck: { type: Boolean, default: false }
});

const palette = ["#1976d2", "#26a69a", "#7e57c2", "#ef6c00", "#c2185b", "#00897b"];

const max = computed(() => Math.max(1, ...props.stages.map((s) => Number(s.value) || 0)));

// Index of the stage immediately after the largest drop.
const bottleneckIndex = computed(() => {
  if (!props.highlightBottleneck || props.stages.length < 2) return -1;
  let worst = -1;
  let worstDrop = -Infinity;
  for (let i = 1; i < props.stages.length; i++) {
    const drop = (Number(props.stages[i - 1].value) || 0) - (Number(props.stages[i].value) || 0);
    if (drop > worstDrop) { worstDrop = drop; worst = i; }
  }
  return worstDrop > 0 ? worst : -1;
});

const rows = computed(() =>
  props.stages.map((s, i) => ({
    label: s.label,
    value: Number(s.value) || 0,
    width: ((Number(s.value) || 0) / max.value) * 100,
    color: s.color || palette[i % palette.length],
    bottleneck: i === bottleneckIndex.value
  })));
</script>

<style scoped>
.funnel-chart__empty { padding: 24px; text-align: center; }
.funnel-chart__row { display: flex; align-items: center; gap: 10px; padding: 4px 0; }
.funnel-chart__label { flex: 0 0 110px; font-size: 15px; color: #455a64; }
.funnel-chart__track { flex: 1 1 auto; background: #eceff1; border-radius: 4px; overflow: hidden; }
.funnel-chart__bar { min-width: 28px; height: 26px; display: flex; align-items: center; justify-content: flex-end; padding: 0 8px; border-radius: 4px; transition: width 0.3s ease; }
.funnel-chart__value { color: #fff; font-size: 14px; font-weight: 600; }
.funnel-chart__row--bottleneck .funnel-chart__track { outline: 2px solid var(--q-negative); }
.funnel-chart__flag { flex: 0 0 auto; }
</style>
