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
    <div v-if="!customer" class="text-grey-6 q-pa-md text-center">No data</div>
    <div v-else class="row q-col-gutter-sm">
      <div v-for="card in cards" :key="card.key" class="col-6 col-sm-4">
        <q-card flat bordered class="kpi-card">
          <q-card-section class="q-pa-sm">
            <div class="row items-center no-wrap">
              <q-icon :name="card.icon" :color="card.color" size="22px" class="q-mr-sm" />
              <div class="col">
                <div class="text-caption text-grey-7 ellipsis">{{ card.label }}</div>
                <div class="row items-center no-wrap">
                  <div class="text-h6 text-weight-bold" :class="card.valueClass">{{ card.value }}</div>
                  <q-chip
                    v-if="card.trendPct != null"
                    dense
                    square
                    :color="card.trendGood ? 'positive' : 'negative'"
                    text-color="white"
                    :icon="card.trendPct >= 0 ? 'o_arrow_upward' : 'o_arrow_downward'"
                    class="q-ml-xs text-caption"
                  >
                    {{ Math.abs(card.trendPct) }}%
                  </q-chip>
                </div>
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
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Cross-Tenant Customer KPIs" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  customer: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

// Trend metadata: for most metrics an upward trend is good; not so for negative outcomes.
const trend = (pct, upIsGood = true) => {
  if (pct == null) return { trendPct: null };
  const up = pct >= 0;
  return { trendPct: pct, trendGood: upIsGood ? up : !up };
};

const cards = computed(() => {
  const c = props.customer || {};
  return [
    { key: "total", label: "Total", value: c.total ?? 0, icon: "o_groups", color: "primary", valueClass: "text-primary", ...trend(c.totalTrendPct, true) },
    { key: "synced", label: "Synced", value: c.synced ?? 0, icon: "o_cloud_done", color: "positive", valueClass: "text-positive", ...trend(c.syncedTrendPct, true) },
    { key: "pendingApproval", label: "Pending Approval", value: c.pendingApproval ?? 0, icon: "o_pending_actions", color: "orange", valueClass: "text-warning", ...trend(c.pendingApprovalTrendPct, false) },
    { key: "syncFailed", label: "Sync Failed", value: c.syncFailed ?? 0, icon: "o_sync_problem", color: "negative", valueClass: "text-negative", ...trend(c.syncFailedTrendPct, false) },
    { key: "rejected", label: "Rejected", value: c.rejected ?? 0, icon: "o_cancel", color: "negative", valueClass: "text-negative", ...trend(c.rejectedTrendPct, false) }
  ];
});
</script>

<style scoped>
.kpi-card { border-radius: 8px; height: 100%; }
</style>
