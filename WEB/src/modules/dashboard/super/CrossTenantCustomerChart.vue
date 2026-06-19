<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Customers"
    :action-route="{ path: '/customers' }"
    :action-permission="Permissions.CustomersReview"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!byTenant.length && !timeline.length" class="column flex-center q-pa-lg text-grey-6">
      <q-icon name="o_insights" size="32px" class="q-mb-sm" />
      No customer data across tenants.
    </div>
    <div v-else>
      <div class="text-caption text-grey-7 q-mb-xs">Customers by Tenant</div>
      <bar-chart v-if="byTenant.length" :categories="byTenantCats" :series="byTenantSeries" />
      <div v-else class="text-grey-6 q-pa-sm text-center">No tenant breakdown.</div>

      <div class="text-caption text-grey-7 q-mt-md q-mb-xs">Daily Sync</div>
      <line-chart v-if="timeline.length" :labels="timelineLabels" :series="timelineSeries" />
      <div v-else class="text-grey-6 q-pa-sm text-center">No sync timeline.</div>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import BarChart from "components/dashboard/charts/BarChart.vue";
import LineChart from "components/dashboard/charts/LineChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Cross-Tenant Customers" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  customer: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

const byTenant = computed(() => props.customer?.byTenant || []);
const byTenantCats = computed(() => byTenant.value.map((t) => t.tenantName));
const byTenantSeries = computed(() => [
  { name: "Customers", color: "#1976d2", values: byTenant.value.map((t) => Number(t.count) || 0) }
]);

const timeline = computed(() => props.customer?.syncTimeline || []);
const timelineLabels = computed(() => timeline.value.map((p) => p.date));
const timelineSeries = computed(() => [
  { name: "Synced", color: "#21ba45", values: timeline.value.map((p) => Number(p.synced) || 0) },
  { name: "Failed", color: "#c10015", values: timeline.value.map((p) => Number(p.failed) || 0) }
]);
</script>
