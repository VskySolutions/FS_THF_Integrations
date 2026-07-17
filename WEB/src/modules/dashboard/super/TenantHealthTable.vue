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
      default-sort-by="pendingCustomers"
      :default-descending="true"
      class="cursor-pointer"
    >
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

const rows = computed(() => props.tenantHealth || []);

const columns = [
  { name: "tenantName", label: "Tenant", field: "tenantName", align: "left", sortable: true },
  { name: "pendingCustomers", label: "Pending", field: "pendingCustomers", align: "center", sortable: true },
  { name: "activeUsers", label: "Users", field: "activeUsers", align: "center", sortable: true }
];

const goToTenant = (row) => {
  if (row?.tenantId) router.push({ path: `/tenants/${row.tenantId}` });
};
</script>
