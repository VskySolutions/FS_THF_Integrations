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
    <div v-if="!points.length" class="text-grey-6 q-pa-md text-center">No data</div>
    <bar-chart v-else :categories="categories" :series="series" stacked />
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import BarChart from "components/dashboard/charts/BarChart.vue";
import { useDateFormat } from "composables/useDateFormat";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Job Volume Trend" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  volumeChart: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const { formatDate } = useDateFormat();

const points = computed(() => props.volumeChart || []);
const categories = computed(() => points.value.map((p) => formatDate(p.date, p.date)));
const series = computed(() => [
  { name: "Completed", color: "#21ba45", values: points.value.map((p) => Number(p.completed) || 0) },
  { name: "Failed", color: "#c10015", values: points.value.map((p) => Number(p.failed) || 0) },
  { name: "Pending", color: "#f2c037", values: points.value.map((p) => Number(p.pending) || 0) }
]);
</script>
