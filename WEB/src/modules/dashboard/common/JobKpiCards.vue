<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="View Jobs"
    :action-route="{ path: '/jobs' }"
    action-permission="jobs.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!kpis" class="text-grey-6 q-pa-md text-center">No data</div>
    <div v-else class="row q-col-gutter-sm">
      <div v-for="card in cards" :key="card.key" class="col-6">
        <q-card
          flat
          bordered
          class="kpi-card cursor-pointer"
          @click="goToJobs(card.statusFilter)"
        >
          <q-card-section class="q-pa-sm">
            <div class="text-caption text-grey-7">{{ card.label }}</div>
            <div class="text-h5 text-weight-bold" :class="card.valueClass">{{ card.value }}</div>
            <div v-if="card.trendPct != null" class="row items-center text-caption" :class="card.trendClass">
              <q-icon :name="card.trendUp ? 'o_arrow_upward' : 'o_arrow_downward'" size="14px" class="q-mr-xs" />
              <span>{{ Math.abs(card.trendPct) }}%</span>
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Job KPIs" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  kpis: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();

const goToJobs = (status) => {
  router.push({ path: "/jobs", query: status ? { status } : undefined });
};

// For most cards an upward trend is good (green); for Failed an upward trend is bad (red).
const trendMeta = (pct, upIsGood) => {
  if (pct == null) return { trendPct: null };
  const trendUp = pct >= 0;
  const good = upIsGood ? trendUp : !trendUp;
  return { trendPct: pct, trendUp, trendClass: good ? "text-positive" : "text-negative" };
};

const cards = computed(() => {
  const k = props.kpis || {};
  return [
    { key: "total", label: "Total", value: k.total ?? 0, statusFilter: null, valueClass: "text-primary", ...trendMeta(k.totalTrendPct, true) },
    { key: "completed", label: "Completed", value: k.completed ?? 0, statusFilter: "Completed", valueClass: "text-positive", ...trendMeta(k.completedTrendPct, true) },
    { key: "failed", label: "Failed", value: k.failed ?? 0, statusFilter: "Failed", valueClass: "text-negative", ...trendMeta(k.failedTrendPct, false) },
    { key: "pending", label: "Pending", value: k.pending ?? 0, statusFilter: "Created", valueClass: "text-warning", ...trendMeta(k.pendingTrendPct, true) }
  ];
});
</script>

<style scoped>
.kpi-card { border-radius: 8px; transition: box-shadow 0.2s ease; }
.kpi-card:hover { box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12); }
</style>
