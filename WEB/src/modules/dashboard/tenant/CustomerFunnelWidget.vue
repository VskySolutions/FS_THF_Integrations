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
    <div v-if="!hasData" class="column flex-center q-pa-lg text-grey-6">
      <q-icon name="o_filter_alt" size="36px" class="q-mb-sm" />
      <div class="text-subtitle2">No pipeline activity</div>
      <div class="text-caption">Customer requests will appear here as they move through the workflow.</div>
    </div>

    <template v-else>
      <!-- Clickable overlay rows over the funnel stages -->
      <div class="funnel-wrap">
        <funnel-chart :stages="stages" highlight-bottleneck />
        <div class="funnel-clicks">
          <div
            v-for="stage in pipeline"
            :key="stage.key"
            class="funnel-clicks__row cursor-pointer"
            @click="goToStatus(stage.key)"
          >
            <q-tooltip>View {{ stage.label }}</q-tooltip>
          </div>
        </div>
      </div>

      <!-- Exit indicators -->
      <div class="row q-col-gutter-sm q-mt-sm">
        <div v-for="exit in exits" :key="exit.key" class="col-6">
          <q-card
            flat
            bordered
            class="exit-card cursor-pointer"
            @click="goToStatus(exit.key)"
          >
            <q-card-section class="q-pa-sm row items-center no-wrap">
              <q-icon :name="exit.icon" :color="exit.color" size="18px" class="q-mr-sm" />
              <div class="col text-caption text-grey-7">{{ exit.label }}</div>
              <div class="text-weight-bold" :class="`text-${exit.color}`">{{ exit.count }}</div>
            </q-card-section>
          </q-card>
        </div>
      </div>
    </template>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import FunnelChart from "components/dashboard/charts/FunnelChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Customer Funnel" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  funnel: { type: Array, default: () => [] }
});

defineEmits(["retry", "update:collapsed"]);

const router = useRouter();

// Pipeline stages, in workflow order (matches CUSTOMER_STAGES).
const pipeline = [
  { key: "Draft", label: "Draft" },
  { key: "Submitted", label: "Submitted" },
  { key: "UnderReview", label: "Under Review" },
  { key: "PendingApproval", label: "Pending Approval" },
  { key: "Approved", label: "Approved" },
  { key: "Synced", label: "Synced" }
];

const countFor = (stageKey) => {
  const row = props.funnel.find((f) => f.stage === stageKey);
  return Number(row?.count) || 0;
};

const stages = computed(() =>
  pipeline.map((s) => ({ label: s.label, value: countFor(s.key) })));

const exits = computed(() => [
  { key: "Returned", label: "Returned", icon: "o_undo", color: "warning", count: countFor("Returned") },
  { key: "Rejected", label: "Rejected", icon: "o_cancel", color: "negative", count: countFor("Rejected") }
]);

const hasData = computed(() =>
  stages.value.some((s) => s.value > 0) || exits.value.some((e) => e.count > 0));

const goToStatus = (status) => router.push({ name: "customers", query: { status } });
</script>

<style scoped>
.funnel-wrap { position: relative; }
.funnel-clicks { position: absolute; inset: 0; display: flex; flex-direction: column; }
.funnel-clicks__row { flex: 1 1 0; }
.exit-card { border-radius: 8px; }
.exit-card:hover { box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12); }
</style>
