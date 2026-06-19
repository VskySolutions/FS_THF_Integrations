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
    <div v-if="!stages.length" class="column flex-center q-pa-lg text-grey-6">
      <q-icon name="o_filter_alt" size="32px" class="q-mb-sm" />
      No conversion data.
    </div>
    <funnel-chart v-else :stages="stages" highlight-bottleneck />
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import FunnelChart from "components/dashboard/charts/FunnelChart.vue";
import { Permissions } from "composables/usePermissions";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Customer Conversion Funnel" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  customer: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

const stages = computed(() =>
  (props.customer?.funnel || []).map((s) => ({ label: s.stage, value: Number(s.count) || 0 })));
</script>
