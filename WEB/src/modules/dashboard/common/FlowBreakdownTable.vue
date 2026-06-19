<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="View Jobs"
    :action-route="{ path: '/jobs' }"
    action-permission="jobs.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!rows.length" class="text-grey-6 q-pa-md text-center">No data</div>
    <q-markup-table v-else flat dense class="flow-table">
      <thead>
        <tr>
          <th class="text-left">Flow</th>
          <th class="text-right">Completed</th>
          <th class="text-right">Failed</th>
          <th class="text-right">Pending</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="row.flow"
          class="cursor-pointer flow-table__row"
          @click="goToFlow(row.flow)"
        >
          <td class="text-left">{{ row.label || row.flow }}</td>
          <td class="text-right text-positive">{{ row.completed ?? 0 }}</td>
          <td class="text-right text-negative">{{ row.failed ?? 0 }}</td>
          <td class="text-right text-warning">{{ row.pending ?? 0 }}</td>
        </tr>
      </tbody>
    </q-markup-table>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Flow Breakdown" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  flowBreakdown: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const rows = computed(() => props.flowBreakdown || []);

const goToFlow = (flow) => {
  router.push({ path: "/jobs", query: flow ? { flow } : undefined });
};
</script>

<style scoped>
.flow-table__row:hover { background: rgba(25, 118, 210, 0.06); }
</style>
