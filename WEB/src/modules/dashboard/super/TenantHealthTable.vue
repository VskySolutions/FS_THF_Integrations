<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    action-label="Tenants"
    :action-route="{ path: '/tenants' }"
    :action-permission="Permissions.TenantsWrite"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <app-data-table
      :rows="rows"
      :columns="columns"
      row-key="tenantId"
      page-key="dashboard-tenant-health"
      default-sort-by="successRate"
      :default-descending="false"
      class="cursor-pointer"
    >
      <template #body-cell-concur="cellProps">
        <q-td :props="cellProps" @click="goToTenant(cellProps.row)">
          <q-badge :color="cellProps.row.concurConfigured ? 'positive' : 'negative'">
            {{ cellProps.row.concurConfigured ? "Configured" : "Not set" }}
          </q-badge>
        </q-td>
      </template>
      <template #body-cell-maconomy="cellProps">
        <q-td :props="cellProps" @click="goToTenant(cellProps.row)">
          <q-badge :color="cellProps.row.maconomyConfigured ? 'positive' : 'negative'">
            {{ cellProps.row.maconomyConfigured ? "Configured" : "Not set" }}
          </q-badge>
        </q-td>
      </template>
      <template #body-cell-successRate="cellProps">
        <q-td :props="cellProps" :class="cellProps.row.successRate < 90 ? 'bg-red-1 text-negative' : ''" @click="goToTenant(cellProps.row)">
          {{ Math.round(cellProps.row.successRate ?? 0) }}%
        </q-td>
      </template>
      <template #body-cell-lastJobRunUtc="cellProps">
        <q-td :props="cellProps" @click="goToTenant(cellProps.row)">
          {{ relative(cellProps.row.lastJobRunUtc) }}
          <q-tooltip v-if="cellProps.row.lastJobRunUtc">{{ formatDateTime(cellProps.row.lastJobRunUtc) }}</q-tooltip>
        </q-td>
      </template>
      <template #body-cell-pendingCustomers="cellProps">
        <q-td :props="cellProps" :class="cellProps.row.pendingCustomers > 0 ? 'bg-orange-1 text-warning' : ''" @click="goToTenant(cellProps.row)">
          {{ cellProps.row.pendingCustomers ?? 0 }}
        </q-td>
      </template>
      <template #body-cell-tenantName="cellProps">
        <q-td :props="cellProps" @click="goToTenant(cellProps.row)">
          <span class="text-primary text-weight-medium">{{ cellProps.row.tenantName }}</span>
        </q-td>
      </template>
      <template #body-cell-activeUsers="cellProps">
        <q-td :props="cellProps" @click="goToTenant(cellProps.row)">{{ cellProps.row.activeUsers ?? 0 }}</q-td>
      </template>
    </app-data-table>
  </dashboard-widget-wrapper>
</template>

<script setup>
import { computed } from "vue";
import { useRouter } from "vue-router";
import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import { Permissions } from "composables/usePermissions";
import { useDateFormat } from "composables/useDateFormat";

const props = defineProps({
  widgetKey: { type: String, default: "" },
  title: { type: String, default: "Tenant Health" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  tenantHealth: { type: Array, default: () => [] }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const { formatDateTime } = useDateFormat();

const rows = computed(() => props.tenantHealth || []);

const columns = [
  { name: "tenantName", label: "Tenant", field: "tenantName", align: "left", sortable: true },
  { name: "concur", label: "Concur", field: "concurConfigured", align: "center", sortable: true },
  { name: "maconomy", label: "Maconomy", field: "maconomyConfigured", align: "center", sortable: true },
  { name: "successRate", label: "Success", field: "successRate", align: "center", sortable: true },
  { name: "lastJobRunUtc", label: "Last Job Run", field: "lastJobRunUtc", align: "left", sortable: true },
  { name: "pendingCustomers", label: "Pending", field: "pendingCustomers", align: "center", sortable: true },
  { name: "activeUsers", label: "Users", field: "activeUsers", align: "center", sortable: true }
];

const relative = (utc) => {
  if (!utc) return "Never";
  const then = new Date(/[zZ]$|[+-]\d{2}:?\d{2}$/.test(String(utc)) ? utc : `${utc}Z`).getTime();
  if (Number.isNaN(then)) return "—";
  const diff = Date.now() - then;
  const mins = Math.round(diff / 60000);
  if (mins < 1) return "Just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.round(hrs / 24);
  return `${days}d ago`;
};

const goToTenant = (row) => {
  if (row?.tenantId) router.push({ path: `/tenants/${row.tenantId}` });
};
</script>
