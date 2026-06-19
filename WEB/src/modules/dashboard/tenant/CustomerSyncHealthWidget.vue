<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="failedAwaitingRetry > 0"
    action-label="Customers"
    :action-route="{ name: 'customers', query: { status: 'Synced' } }"
    :action-permission="Permissions.CustomersReview"
    @retry="$emit('retry')"
    @update:collapsed="$emit('update:collapsed', $event)"
  >
    <!-- KPI row -->
    <div class="row q-col-gutter-sm items-stretch">
      <div class="col-4">
        <div class="sync-kpi">
          <div class="text-h6 text-weight-bold">{{ h.totalSynced ?? 0 }}</div>
          <div class="text-caption text-grey-7">Total Synced</div>
        </div>
      </div>
      <div class="col-4">
        <div class="sync-kpi">
          <div class="text-h6 text-weight-bold">{{ h.syncedThisMonth ?? 0 }}</div>
          <div class="text-caption text-grey-7">This Month</div>
        </div>
      </div>
      <div class="col-4">
        <gauge-chart :value="h.successRate ?? 0" :threshold="90" label="Success Rate" />
      </div>
      <div class="col-6">
        <div class="sync-kpi sync-kpi--info">
          <div class="text-subtitle1 text-weight-bold text-primary">{{ h.inProgress ?? 0 }}</div>
          <div class="text-caption text-grey-7">In Progress</div>
        </div>
      </div>
      <div class="col-6">
        <div class="sync-kpi" :class="{ 'sync-kpi--alert': failedAwaitingRetry > 0 }">
          <div class="text-subtitle1 text-weight-bold" :class="failedAwaitingRetry > 0 ? 'text-negative' : ''">
            {{ failedAwaitingRetry }}
          </div>
          <div class="text-caption text-grey-7">Failed / Awaiting Retry</div>
        </div>
      </div>
    </div>

    <!-- Daily timeline -->
    <div v-if="timeline.length" class="q-mt-md">
      <div class="text-caption text-grey-7 q-mb-xs">Daily sync timeline</div>
      <bar-chart :categories="timelineLabels" :series="timelineSeries" />
    </div>

    <!-- Recent failures -->
    <div class="q-mt-md">
      <div v-if="!recentFailures.length" class="column flex-center q-pa-md text-positive">
        <q-icon name="o_cloud_done" size="32px" class="q-mb-xs" />
        <div class="text-subtitle2">All syncs successful</div>
      </div>
      <template v-else>
        <div class="text-caption text-grey-7 q-mb-xs">Recent failures</div>
        <q-list separator>
          <q-item v-for="f in recentFailures.slice(0, 3)" :key="f.requestId">
            <q-item-section>
              <q-item-label class="text-weight-medium">{{ f.customerRequestNumber }}</q-item-label>
              <q-item-label caption>{{ f.companyName || "—" }}</q-item-label>
              <q-item-label caption class="text-negative ellipsis">{{ f.errorMessage }}</q-item-label>
              <q-item-label caption>{{ formatDateTime(f.failedAtUtc) }}</q-item-label>
            </q-item-section>
            <q-item-section v-if="has(Permissions.CustomersReview)" side>
              <q-btn
                flat
                dense
                no-caps
                size="sm"
                color="primary"
                icon="o_replay"
                label="Retry Sync"
                :loading="retrying === f.requestId"
                @click="retrySync(f)"
              />
            </q-item-section>
          </q-item>
        </q-list>
      </template>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed, ref } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import GaugeChart from "components/dashboard/charts/GaugeChart.vue";
import BarChart from "components/dashboard/charts/BarChart.vue";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useDateFormat } from "composables/useDateFormat";
import { useNotify } from "composables/useNotify";
import { customerApi, getApiErrorMessage } from "services/api";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Sync Health" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  syncHealth: { type: Object, default: null }
});

const emit = defineEmits(["retry", "update:collapsed"]);

const { has } = usePermissions();
const { formatDateTime } = useDateFormat();
const notify = useNotify();

const h = computed(() => props.syncHealth || {});
const failedAwaitingRetry = computed(() => Number(h.value.failedAwaitingRetry) || 0);
const timeline = computed(() => h.value.timeline || []);
const recentFailures = computed(() => h.value.recentFailures || []);

// Show day-of-month as the bar category label.
const timelineLabels = computed(() =>
  timeline.value.map((t) => {
    const d = new Date(t.date);
    return Number.isNaN(d.getTime()) ? String(t.date) : `${d.getMonth() + 1}/${d.getDate()}`;
  }));

const timelineSeries = computed(() => [
  { name: "Synced", color: "#21ba45", values: timeline.value.map((t) => Number(t.synced) || 0) },
  { name: "Failed", color: "#c10015", values: timeline.value.map((t) => Number(t.failed) || 0) }
]);

const retrying = ref(null);
const retrySync = async (failure) => {
  retrying.value = failure.requestId;
  try {
    await customerApi.retrySync(failure.requestId);
    notify.success("Sync retry queued.");
    emit("retry");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    retrying.value = null;
  }
};
</script>

<style scoped>
.sync-kpi { text-align: center; padding: 6px 4px; border-radius: 8px; }
.sync-kpi--info { background: #e3f2fd; }
.sync-kpi--alert { background: #ffebee; }
</style>
