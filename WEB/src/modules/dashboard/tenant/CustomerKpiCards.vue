<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Customers"
    :action-route="{ name: 'customers' }"
    :action-permission="Permissions.CustomersReview"
    @retry="$emit('retry')"
    @update:collapsed="$emit('update:collapsed', $event)"
  >
    <div class="row q-col-gutter-sm">
      <div v-for="card in cards" :key="card.key" class="col-12 col-sm-6">
        <q-card
          flat
          bordered
          class="kpi-card cursor-pointer"
          :class="{ 'kpi-card--highlight': card.highlight }"
          @click="goToStatus(card.status)"
        >
          <q-card-section class="q-pa-sm">
            <div class="row items-center no-wrap">
              <q-icon :name="card.icon" :color="card.color" size="22px" class="q-mr-sm" />
              <div class="col">
                <div class="text-caption text-grey-7 ellipsis">{{ card.label }}</div>
                <div class="row items-center no-wrap">
                  <div class="text-h6 text-weight-bold">{{ card.value }}</div>
                  <q-chip
                    v-if="card.trendPct !== null && card.trendPct !== undefined"
                    dense
                    square
                    :color="card.trendPct >= 0 ? 'positive' : 'negative'"
                    text-color="white"
                    :icon="card.trendPct >= 0 ? 'o_arrow_upward' : 'o_arrow_downward'"
                    class="q-ml-sm text-caption"
                  >
                    {{ Math.abs(card.trendPct) }}%
                  </q-chip>
                </div>
              </div>
            </div>
            <q-btn
              v-if="card.key === 'syncFailed' && card.value > 0 && has(Permissions.CustomersReview)"
              flat
              dense
              no-caps
              size="sm"
              color="negative"
              icon="o_replay"
              label="Retry All"
              class="q-mt-xs"
              @click.stop="goToStatus('Failed')"
            />
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
import { usePermissions, Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Customer KPIs" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  kpis: { type: Object, default: null }
});

defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const { has } = usePermissions();

const k = computed(() => props.kpis || {});

const cards = computed(() => [
  { key: "total", label: "Total Requests", value: k.value.total ?? 0, icon: "o_groups", color: "primary", status: null, trendPct: k.value.totalTrendPct ?? null },
  { key: "synced", label: "Synced", value: k.value.synced ?? 0, icon: "o_cloud_done", color: "positive", status: "Synced", trendPct: k.value.syncedTrendPct ?? null },
  { key: "pendingAction", label: "Pending Action", value: k.value.pendingAction ?? 0, icon: "o_pending_actions", color: "orange", status: "PendingApproval", highlight: true, trendPct: null },
  { key: "syncFailed", label: "Sync Failed", value: k.value.syncFailed ?? 0, icon: "o_sync_problem", color: "negative", status: "Failed", trendPct: null },
  { key: "rejected", label: "Rejected", value: k.value.rejected ?? 0, icon: "o_cancel", color: "negative", status: "Rejected", trendPct: null }
]);

const goToStatus = (status) => {
  router.push(status ? { name: "customers", query: { status } } : { name: "customers" });
};
</script>

<style scoped>
.kpi-card { border-radius: 10px; transition: box-shadow 0.2s ease; }
.kpi-card:hover { box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12); }
.kpi-card--highlight { border-color: var(--q-warning); background: #fff8e1; }
</style>
