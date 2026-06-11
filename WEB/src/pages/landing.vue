<template>
  <q-page padding>
    <div class="q-mx-auto" style="max-width: 1100px;">
      <!-- Hero banner -->
      <q-card flat class="hero-banner text-white q-pa-lg q-mb-lg">
        <div class="row items-center no-wrap justify-between">
          <div class="row items-center no-wrap">
            <q-icon name="o_sync_alt" size="56px" class="q-mr-md gt-xs" />
            <div>
              <div class="text-h4 text-weight-bold">
                Welcome to THF Integration<span v-if="displayName">, {{ displayName }}</span>
              </div>
              <div class="text-subtitle1 q-mt-xs" style="opacity: 0.9;">
                Concur &rarr; Maconomy integration platform
              </div>
            </div>
          </div>
          <q-btn
            v-if="!isLoggedIn"
            unelevated
            color="white"
            text-color="primary"
            no-caps
            icon="o_login"
            label="Login"
            :to="{ name: 'login' }"
          />
        </div>
      </q-card>

      <!-- Dashboard (admins) -->
      <template v-if="isLoggedIn && canViewStats">
        <div class="row items-center q-mb-sm">
          <div class="text-subtitle1 text-weight-medium text-grey-8">Today's activity</div>
          <q-space />
          <q-btn flat round dense icon="o_refresh" :loading="loadingStats" @click="loadStats" />
        </div>

        <q-banner v-if="healthWarning" dense class="bg-red-1 text-negative q-mb-md" rounded>
          <template #avatar><q-icon name="o_warning" color="negative" /></template>
          One or more components are unhealthy.
          <template #action>
            <q-btn flat dense no-caps color="negative" label="View health" :to="{ name: 'health' }" />
          </template>
        </q-banner>

        <div class="row q-col-gutter-md q-mb-lg">
          <div v-for="stat in stats" :key="stat.label" class="col-6 col-md-3">
            <q-card flat bordered class="stat-card" :class="{ 'cursor-pointer': stat.to }" @click="stat.to && $router.push(stat.to)">
              <q-card-section>
                <div class="row items-center justify-between">
                  <q-icon :name="stat.icon" size="28px" :color="stat.color" />
                  <div class="text-h4 text-weight-bold">{{ stat.value }}</div>
                </div>
                <div class="text-body2 text-grey-7 q-mt-xs">{{ stat.label }}</div>
              </q-card-section>
            </q-card>
          </div>
        </div>
      </template>

      <!-- Quick links -->
      <template v-if="isLoggedIn">
        <div class="text-subtitle1 text-weight-medium text-grey-8 q-mb-sm">Quick links</div>
        <div class="row q-col-gutter-md">
          <div v-for="card in cards" :key="card.title" class="col-12 col-sm-6 col-md-4">
            <q-card v-ripple flat bordered class="quick-card cursor-pointer full-height" @click="$router.push(card.to)">
              <q-card-section>
                <div class="row items-center justify-between no-wrap">
                  <q-avatar rounded size="48px" color="primary" text-color="white">
                    <q-icon :name="card.icon" size="26px" />
                  </q-avatar>
                  <q-icon name="o_arrow_forward" color="grey-5" />
                </div>
                <div class="text-h6 q-mt-md">{{ card.title }}</div>
                <div class="text-body2 text-grey-7">{{ card.description }}</div>
              </q-card-section>
            </q-card>
          </div>
        </div>
      </template>
    </div>
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { LocalStorage, date } from "quasar";
import { useAuthStore } from "stores/auth";
import { usePermissions, Permissions } from "composables/usePermissions";
import { jobApi, adminApi } from "services/api";

const authStore = useAuthStore();
const { has } = usePermissions();
const user = authStore.user;
const displayName = user?.displayName || user?.firstName || "";
const isLoggedIn = !!LocalStorage.getItem("token");
const canViewStats = computed(() => has(Permissions.JobsRead));

const cards = [
  { title: "Integration Jobs", description: "Trigger imports and track job status.", icon: "o_sync", to: "/jobs" },
  { title: "Mapping Config", description: "Manage field mapping rules.", icon: "o_swap_horiz", to: "/mappings" },
  { title: "Account", description: "Manage your account and profile.", icon: "o_manage_accounts", to: "/account" }
];

// ---- Dashboard stats ----
const counts = ref({ total: 0, succeeded: 0, failed: 0, pendingRetry: 0 });
const loadingStats = ref(false);
const healthWarning = ref(false);

const stats = computed(() => [
  { label: "Total today", value: counts.value.total, icon: "o_summarize", color: "primary" },
  { label: "Succeeded", value: counts.value.succeeded, icon: "o_check_circle", color: "positive" },
  { label: "Failed", value: counts.value.failed, icon: "o_error", color: "negative", to: "/jobs" },
  { label: "Pending retry", value: counts.value.pendingRetry, icon: "o_replay", color: "orange" }
]);

const today = () => date.formatDate(Date.now(), "YYYY-MM-DD");

const loadStats = async () => {
  if (!canViewStats.value) return;
  loadingStats.value = true;
  const from = today();
  try {
    const [total, succeeded, failed, retries] = await Promise.all([
      jobApi.list({ page: 1, limit: 1, fromDate: from }),
      jobApi.list({ page: 1, limit: 1, fromDate: from, status: "Completed" }),
      jobApi.list({ page: 1, limit: 1, fromDate: from, status: "Failed" }),
      jobApi.retries({ page: 1, limit: 1 })
    ]);
    counts.value = {
      total: total?.meta?.totalRecords ?? 0,
      succeeded: succeeded?.meta?.totalRecords ?? 0,
      failed: failed?.meta?.totalRecords ?? 0,
      pendingRetry: retries?.meta?.totalRecords ?? 0
    };
  } catch {
    // counters stay at last value; non-blocking
  } finally {
    loadingStats.value = false;
  }
};

const loadHealth = async () => {
  if (!canViewStats.value) return;
  try {
    const report = await adminApi.health();
    healthWarning.value = (report?.components || []).some((c) => c.status && c.status !== "Healthy");
  } catch {
    healthWarning.value = false;
  }
};

let pollTimer = null;
const refreshAll = () => { loadStats(); loadHealth(); };
const onTenantSwitched = () => refreshAll();

onMounted(() => {
  if (isLoggedIn && canViewStats.value) {
    refreshAll();
    pollTimer = setInterval(refreshAll, 60000);
    window.addEventListener("tenant-switched", onTenantSwitched);
  }
});
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer);
  window.removeEventListener("tenant-switched", onTenantSwitched);
});
</script>

<style scoped>
.hero-banner {
  border-radius: 16px;
  background: linear-gradient(120deg, var(--q-primary), #5b8def);
}
.quick-card, .stat-card {
  border-radius: 14px;
  transition: box-shadow 0.2s ease, transform 0.2s ease;
}
.quick-card:hover, .stat-card.cursor-pointer:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.1);
}
</style>
