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
    <div v-if="!hasData" class="text-grey-6 q-pa-md text-center">No Data</div>
    <gauge-chart v-else :value="rate" :threshold="90" label="Success rate" />
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import GaugeChart from "components/dashboard/charts/GaugeChart.vue";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Job Success Rate" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  successRate: { type: [Number, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

// When there were no jobs the API reports null/0 success rate; treat that as an empty state.
const rate = computed(() => Number(props.successRate) || 0);
const hasData = computed(() => props.successRate != null && Number(props.successRate) > 0);
</script>
