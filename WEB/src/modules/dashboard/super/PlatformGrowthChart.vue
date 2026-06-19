<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Tenants"
    :action-route="{ path: '/tenants' }"
    :action-permission="Permissions.TenantsWrite"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!points.length" class="column flex-center q-pa-lg text-grey-6">
      <q-icon name="o_show_chart" size="32px" class="q-mb-sm" />
      No growth data for this period.
    </div>
    <line-chart v-else :labels="labels" :series="series" />
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import LineChart from "components/dashboard/charts/LineChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Platform Growth" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  growth: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const points = computed(() => props.growth || []);
const labels = computed(() => points.value.map((p) => p.date));
const series = computed(() => [
  { name: "Tenants", color: "#1976d2", values: points.value.map((p) => Number(p.tenants) || 0) },
  { name: "Users", color: "#26a69a", values: points.value.map((p) => Number(p.users) || 0) }
]);
</script>
