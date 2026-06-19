<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Users"
    :action-route="{ path: '/users' }"
    action-permission="users.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!hasData" class="text-grey-6 q-pa-md text-center">No data</div>
    <div v-else class="role-dist">
      <donut-chart :segments="segments" :center-label="`${total}`" />
      <ul class="role-dist__legend">
        <li
          v-for="seg in segments"
          :key="seg.role"
          class="role-dist__item cursor-pointer"
          @click="goToRole(seg.role)"
        >
          <span class="role-dist__swatch" :style="{ background: seg.color }" />
          <span class="role-dist__label" :class="{ 'text-italic text-grey-7': seg.unassigned }">{{ seg.label }}</span>
          <span class="role-dist__value text-grey-7">{{ seg.value }}</span>
        </li>
      </ul>
    </div>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import DonutChart from "components/dashboard/charts/DonutChart.vue";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Role Distribution" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  roleDistribution: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();

const PALETTE = ["#1976d2", "#26a69a", "#7e57c2", "#ef6c00", "#c2185b", "#00897b", "#5c6bc0", "#43a047"];
const UNASSIGNED_COLOR = "#90a4ae";

const isUnassigned = (role) => {
  const r = String(role || "").trim().toLowerCase();
  return !r || r === "unassigned" || r === "none";
};

const segments = computed(() => {
  let pi = 0;
  return (props.roleDistribution || []).map((d) => {
    const unassigned = isUnassigned(d.role);
    const color = unassigned ? UNASSIGNED_COLOR : PALETTE[pi++ % PALETTE.length];
    return {
      role: d.role,
      label: unassigned ? "Unassigned" : (d.role || "—"),
      value: Number(d.count) || 0,
      color,
      unassigned
    };
  });
});

const total = computed(() => segments.value.reduce((s, x) => s + x.value, 0));
const hasData = computed(() => total.value > 0);

const goToRole = (role) => {
  if (isUnassigned(role)) {
    router.push({ path: "/users", query: { role: "unassigned" } });
  } else {
    router.push({ path: "/users", query: { role } });
  }
};
</script>

<style scoped>
.role-dist { display: flex; flex-wrap: wrap; align-items: center; gap: 16px; }
.role-dist__legend { list-style: none; margin: 0; padding: 0; flex: 1 1 140px; }
.role-dist__item { display: flex; align-items: center; gap: 8px; padding: 3px 0; font-size: 15px; }
.role-dist__item:hover { color: var(--q-primary); }
.role-dist__swatch { width: 12px; height: 12px; border-radius: 3px; flex: 0 0 auto; }
.role-dist__value { margin-left: auto; font-weight: 600; }
</style>
