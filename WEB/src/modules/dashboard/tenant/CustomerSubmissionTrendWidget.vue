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
      <q-icon name="o_show_chart" size="36px" class="q-mb-sm" />
      <div class="text-subtitle2">No submission data</div>
      <div class="text-caption">Weekly submissions and approvals will appear here.</div>
    </div>

    <template v-else>
      <line-chart :labels="weekLabels" :series="series" />

      <div v-if="topSubmitters.length" class="q-mt-md">
        <div class="text-caption text-grey-7 q-mb-xs">Top 5 submitters</div>
        <q-list dense>
          <q-item v-for="(s, i) in topFive" :key="s.submitterId">
            <q-item-section avatar>
              <q-avatar size="26px" color="primary" text-color="white" class="text-caption">{{ i + 1 }}</q-avatar>
            </q-item-section>
            <q-item-section>{{ s.submitterName || "—" }}</q-item-section>
            <q-item-section side>
              <q-badge color="primary" text-color="white">{{ s.count }}</q-badge>
            </q-item-section>
          </q-item>
        </q-list>
      </div>
    </template>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import LineChart from "components/dashboard/charts/LineChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Submission Trend" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  submissionTrend: { type: Array, default: () => [] },
  topSubmitters: { type: Array, default: () => [] }
});

defineEmits(["retry", "update:collapsed"]);

const weekLabels = computed(() =>
  props.submissionTrend.map((w) => {
    const d = new Date(w.weekStart);
    return Number.isNaN(d.getTime()) ? String(w.weekStart) : `${d.getMonth() + 1}/${d.getDate()}`;
  }));

const series = computed(() => [
  { name: "Submitted", color: "#1976d2", values: props.submissionTrend.map((w) => Number(w.submitted) || 0) },
  { name: "Approved", color: "#21ba45", values: props.submissionTrend.map((w) => Number(w.approved) || 0) }
]);

const topFive = computed(() => props.topSubmitters.slice(0, 5));

const hasData = computed(() =>
  props.submissionTrend.some((w) => (Number(w.submitted) || 0) > 0 || (Number(w.approved) || 0) > 0));
</script>
