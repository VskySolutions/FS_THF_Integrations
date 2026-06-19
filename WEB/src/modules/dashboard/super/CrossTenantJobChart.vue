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
    <div v-if="!rows.length" class="column flex-center q-pa-lg text-grey-6">
      <q-icon name="o_bar_chart" size="32px" class="q-mb-sm" />
      No job activity across tenants.
    </div>
    <div v-else>
      <bar-chart :categories="categories" :series="series" stacked />
      <!-- Per-tenant navigation (BarChart bars do not emit clicks). -->
      <div class="row q-gutter-xs q-mt-sm">
        <q-chip
          v-for="t in rows"
          :key="t.tenantId"
          dense
          clickable
          color="blue-1"
          text-color="primary"
          icon="o_north_east"
          @click="goToTenant(t.tenantId)"
        >
          {{ t.tenantName }}
        </q-chip>
      </div>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import BarChart from "components/dashboard/charts/BarChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Cross-Tenant Jobs" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  crossTenantJobs: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();

const rows = computed(() => (props.crossTenantJobs || []).slice(0, 10));
const categories = computed(() => rows.value.map((t) => t.tenantName));
const series = computed(() => [
  { name: "Completed", color: "#21ba45", values: rows.value.map((t) => Number(t.completed) || 0) },
  { name: "Failed", color: "#c10015", values: rows.value.map((t) => Number(t.failed) || 0) },
  { name: "Pending", color: "#f2c037", values: rows.value.map((t) => Number(t.pending) || 0) }
]);

const goToTenant = (tenantId) => {
  if (tenantId) router.push({ path: `/tenants/${tenantId}` });
};
</script>
