<template>
  <q-page padding>
    <app-breadcrumbs :items="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Health' }]" />

    <div class="row items-center q-mb-md">
      <div class="text-h5 text-weight-bold">System Health</div>
      <q-space />
      <q-btn flat round dense icon="o_refresh" :loading="loading" @click="load" />
    </div>

    <div v-if="loading && !components.length" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <q-banner v-else-if="error" dense class="bg-red-1 text-negative q-mb-md" rounded>
      <template #avatar><q-icon name="o_error" color="negative" /></template>
      Could not load health status.
      <template #action><q-btn flat dense no-caps color="negative" label="Retry" @click="load" /></template>
    </q-banner>

    <template v-else>
      <q-banner v-if="allHealthy" dense class="bg-green-1 text-positive q-mb-md" rounded>
        <template #avatar><q-icon name="o_check_circle" color="positive" /></template>
        All systems operational.
      </q-banner>

      <q-card flat bordered class="health-card">
        <q-list separator>
          <q-item v-for="c in components" :key="c.name">
            <q-item-section avatar>
              <q-icon :name="componentIcon(c.name)" color="primary" />
            </q-item-section>
            <q-item-section>
              <q-item-label class="text-weight-medium">{{ c.name }}</q-item-label>
            </q-item-section>
            <q-item-section side class="row items-center q-gutter-sm">
              <q-chip dense outline color="grey-7">checked {{ checkedAt }}</q-chip>
              <q-badge :color="statusColor(c.status)">{{ c.status }}</q-badge>
            </q-item-section>
          </q-item>
        </q-list>
      </q-card>
    </template>
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { date } from "quasar";
import { adminApi } from "services/api";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";

const components = ref([]);
const overall = ref(null);
const loading = ref(false);
const error = ref(false);
const checkedAt = ref("");

const statusColor = (status) => ({ Healthy: "positive", Degraded: "orange", Unhealthy: "negative" }[status] || "grey");
const allHealthy = computed(() => components.value.length && components.value.every((c) => c.status === "Healthy"));
const componentIcon = (name) => {
  const n = (name || "").toLowerCase();
  if (n.includes("sql") || n.includes("database")) return "o_storage";
  if (n.includes("concur")) return "o_receipt_long";
  if (n.includes("maconomy")) return "o_account_balance";
  return "o_dns";
};

const load = async () => {
  loading.value = true;
  error.value = false;
  try {
    const report = await adminApi.health();
    overall.value = report?.status || null;
    components.value = report?.components || [];
    checkedAt.value = date.formatDate(Date.now(), "hh:mm:ss A");
  } catch {
    error.value = true;
  } finally {
    loading.value = false;
  }
};

onMounted(load);
</script>

<style scoped>
.health-card {
  border-radius: 12px;
}
</style>
