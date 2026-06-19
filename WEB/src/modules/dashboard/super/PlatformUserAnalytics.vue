<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Users"
    :action-route="{ path: '/users' }"
    :action-permission="Permissions.UsersRead"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!analytics" class="text-grey-6 q-pa-md text-center">No data</div>
    <div v-else>
      <!-- KPI row -->
      <div class="row q-col-gutter-sm">
        <div v-for="card in cards" :key="card.key" class="col-6 col-sm-4">
          <q-card flat bordered class="kpi-card">
            <q-card-section class="q-pa-sm">
              <div class="text-caption text-grey-7 ellipsis">{{ card.label }}</div>
              <div class="text-h6 text-weight-bold" :class="card.valueClass">{{ card.value }}</div>
            </q-card-section>
          </q-card>
        </div>
      </div>

      <!-- User growth -->
      <div class="text-caption text-grey-7 q-mt-md q-mb-xs">User Growth</div>
      <line-chart v-if="growthSeries[0].values.length" :labels="growthLabels" :series="growthSeries" />
      <div v-else class="text-grey-6 q-pa-sm text-center">No growth data.</div>

      <!-- Users by tenant -->
      <div class="text-caption text-grey-7 q-mt-md q-mb-xs">Users by Tenant</div>
      <bar-chart v-if="byTenantCats.length" :categories="byTenantCats" :series="byTenantSeries" />
      <div v-else class="text-grey-6 q-pa-sm text-center">No tenant breakdown.</div>

      <!-- Activity feed -->
      <div class="text-caption text-grey-7 q-mt-md q-mb-xs">Recent Activity</div>
      <q-list v-if="activity.length" dense separator>
        <q-item v-for="(item, i) in activity" :key="i">
          <q-item-section avatar><q-icon name="o_history" color="grey-6" size="18px" /></q-item-section>
          <q-item-section>
            <q-item-label>{{ item.description || item.message || item.action }}</q-item-label>
            <q-item-label v-if="item.userName || item.tenantName" caption>
              {{ [item.userName, item.tenantName].filter(Boolean).join(" · ") }}
            </q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
      <div v-else class="text-grey-6 q-pa-sm text-center">No recent activity.</div>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import LineChart from "components/dashboard/charts/LineChart.vue";
import BarChart from "components/dashboard/charts/BarChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "User Analytics" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  userAnalytics: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

const analytics = computed(() => props.userAnalytics);

const cards = computed(() => {
  const a = analytics.value || {};
  return [
    { key: "totalActive", label: "Active Users", value: a.totalActive ?? 0, valueClass: "text-primary" },
    { key: "loggedInToday", label: "Logged In Today", value: a.loggedInToday ?? 0, valueClass: "text-positive" },
    { key: "pendingFirstLogin", label: "Pending First Login", value: a.pendingFirstLogin ?? 0, valueClass: "text-warning" },
    { key: "noRole", label: "No Role", value: a.noRole ?? 0, valueClass: (a.noRole ?? 0) > 0 ? "text-negative" : "" },
    { key: "newThisMonth", label: "New This Month", value: a.newThisMonth ?? 0, valueClass: "text-primary" }
  ];
});

const growth = computed(() => analytics.value?.growth || []);
const growthLabels = computed(() => growth.value.map((p) => p.date));
const growthSeries = computed(() => [
  { name: "Users", color: "#1976d2", values: growth.value.map((p) => Number(p.users) || 0) }
]);

const byTenant = computed(() => analytics.value?.byTenant || []);
const byTenantCats = computed(() => byTenant.value.map((t) => t.tenantName));
const byTenantSeries = computed(() => [
  { name: "Users", color: "#26a69a", values: byTenant.value.map((t) => Number(t.count) || 0) }
]);

const activity = computed(() => analytics.value?.activityFeed || []);
</script>

<style scoped>
.kpi-card { border-radius: 8px; height: 100%; }
</style>
