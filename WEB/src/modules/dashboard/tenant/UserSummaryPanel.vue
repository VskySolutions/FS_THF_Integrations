<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="hasAttention"
    action-label="Users"
    :action-route="{ path: '/users' }"
    action-permission="users.read"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!kpis" class="text-grey-6 q-pa-md text-center">No data</div>
    <div v-else class="row q-col-gutter-sm">
      <div v-for="card in cards" :key="card.key" class="col-4">
        <q-card
          flat
          bordered
          class="user-kpi"
          :class="[{ 'cursor-pointer': card.query }, card.warn ? 'user-kpi--warn' : '']"
          @click="card.query && goToUsers(card.query)"
        >
          <q-card-section class="q-pa-sm text-center">
            <div class="text-h6 text-weight-bold" :class="card.warn ? 'text-warning' : 'text-primary'">
              {{ card.value }}
            </div>
            <div class="text-caption text-grey-7 row items-center justify-center no-wrap">
              <q-icon v-if="card.warn" name="o_warning" color="warning" size="14px" class="q-mr-xs" />
              {{ card.label }}
            </div>
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

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "User Summary" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  kpis: { type: [Object, null], default: null },
  activityFeed: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const goToUsers = (query) => router.push({ path: "/users", query });

const cards = computed(() => {
  const k = props.kpis || {};
  return [
    { key: "total", label: "Total", value: k.total ?? 0, query: {} },
    { key: "loggedInToday", label: "Logged in today", value: k.loggedInToday ?? 0, query: null },
    { key: "activeThisWeek", label: "Active this week", value: k.activeThisWeek ?? 0, query: null },
    { key: "inactive30Days", label: "Inactive 30d", value: k.inactive30Days ?? 0, query: { inactive: "30" }, warn: (k.inactive30Days ?? 0) > 0 },
    { key: "pendingFirstLogin", label: "Pending first login", value: k.pendingFirstLogin ?? 0, query: { pending: "1" }, warn: (k.pendingFirstLogin ?? 0) > 0 },
    { key: "newThisMonth", label: "New this month", value: k.newThisMonth ?? 0, query: null }
  ];
});

const hasAttention = computed(() => cards.value.some((c) => c.warn));
</script>

<style scoped>
.user-kpi { border-radius: 8px; transition: box-shadow 0.2s ease; height: 100%; }
.user-kpi.cursor-pointer:hover { box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12); }
.user-kpi--warn { border-color: var(--q-warning); }
</style>
