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
    <div v-if="!tenantKpis" class="text-grey-6 q-pa-md text-center">No data</div>
    <div v-else class="row q-col-gutter-sm">
      <div v-for="card in cards" :key="card.key" class="col-6 col-sm-4">
        <q-card
          flat
          bordered
          class="kpi-card"
          :class="[{ 'cursor-pointer': card.route, 'kpi-card--highlight': card.highlight }]"
          @click="card.route && router.push(card.route)"
        >
          <q-card-section class="q-pa-sm">
            <div class="row items-center no-wrap">
              <q-icon :name="card.icon" :color="card.color" size="22px" class="q-mr-sm" />
              <div class="col">
                <div class="text-caption text-grey-7 ellipsis">{{ card.label }}</div>
                <div class="text-h6 text-weight-bold" :class="card.valueClass">{{ card.value }}</div>
              </div>
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
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Tenant KPIs" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  tenantKpis: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();

const cards = computed(() => {
  const k = props.tenantKpis || {};
  return [
    { key: "activeTenants", label: "Active Tenants", value: k.activeTenants ?? 0, icon: "o_apartment", color: "positive", route: { path: "/tenants" } },
    { key: "inactiveTenants", label: "Inactive Tenants", value: k.inactiveTenants ?? 0, icon: "o_domain_disabled", color: "grey", route: { path: "/tenants" } },
    { key: "archivedTenants", label: "Archived Tenants", value: k.archivedTenants ?? 0, icon: "o_inventory_2", color: "grey", route: { path: "/tenants" } },
    { key: "totalUsers", label: "Total Users", value: k.totalUsers ?? 0, icon: "o_group", color: "primary", route: { path: "/users" } },
    { key: "jobsToday", label: "Jobs Today", value: k.jobsToday ?? 0, icon: "o_sync", color: "primary" },
    { key: "platformSuccessRate", label: "Success Rate", value: `${Math.round(k.platformSuccessRate ?? 0)}%`, icon: "o_verified", color: (k.platformSuccessRate ?? 0) >= 90 ? "positive" : "warning", valueClass: (k.platformSuccessRate ?? 0) >= 90 ? "text-positive" : "text-warning" }
  ];
});
</script>

<style scoped>
.kpi-card { border-radius: 8px; transition: box-shadow 0.2s ease; height: 100%; }
.kpi-card.cursor-pointer:hover { box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12); }
.kpi-card--highlight { border-color: var(--q-warning); background: #fff8e1; }
</style>
