<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="!allOperational"
    action-label="Health"
    :action-route="{ path: '/health' }"
    action-permission="health.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <q-banner
      v-if="allOperational"
      dense
      rounded
      class="bg-green-1 text-positive q-mb-sm"
    >
      <template #avatar><q-icon name="o_check_circle" color="positive" /></template>
      All Systems Operational
    </q-banner>
    <q-banner
      v-else
      dense
      rounded
      class="bg-orange-1 text-warning q-mb-sm"
    >
      <template #avatar><q-icon name="o_warning" color="warning" /></template>
      Some components need attention
    </q-banner>

    <div v-if="!components.length" class="text-grey-6 q-pa-md text-center">No data</div>
    <q-list v-else dense>
      <q-item v-for="c in components" :key="c.name">
        <q-item-section>
          <q-item-label>{{ c.name }}</q-item-label>
          <q-item-label v-if="c.description" caption>{{ c.description }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-badge :color="statusColor(c.status)" :label="c.status || 'Unknown'" />
        </q-item-section>
      </q-item>
    </q-list>
  </dashboard-widget-wrapper>
</template>

<script setup>
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";

defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "System Health" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  status: { type: [String, null], default: null },
  components: { type: Array, default: () => [] },
  allOperational: { type: Boolean, default: false }
});
defineEmits(["retry", "update:collapsed"]);

const statusColor = (status) => {
  const s = String(status || "").toLowerCase();
  if (s === "healthy") return "positive";
  if (s === "degraded") return "warning";
  if (s === "unhealthy") return "negative";
  return "grey";
};
</script>
