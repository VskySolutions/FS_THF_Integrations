<template>
  <dashboard-widget-wrapper
    :widget-key="widgetKey"
    :title="title"
    :loading="loading"
    :error="error"
    :collapsed="collapsed"
    :alert="issues.length > 0"
    action-label="Customers"
    :action-route="{ path: '/customers' }"
    :action-permission="Permissions.CustomersReview"
    @retry="$emit('retry')"
    @update:collapsed="(v) => $emit('update:collapsed', v)"
  >
    <div v-if="!issues.length" class="column flex-center q-pa-lg text-positive">
      <q-icon name="o_check_circle" size="32px" class="q-mb-sm" />
      <div class="text-subtitle2">All pipelines healthy ✓</div>
    </div>
    <app-data-table
      v-else
      :rows="issues"
      :columns="columns"
      row-key="tenantId"
      page-key="dashboard-customer-issues"
      default-sort-by="tenantName"
      :default-descending="false"
      class="cursor-pointer"
    >
      <template #body-cell-tenantName="cellProps">
        <q-td :props="cellProps" @click="goToTenant(cellProps.row)">
          <span class="text-primary text-weight-medium">{{ cellProps.row.tenantName }}</span>
        </q-td>
      </template>
      <template #body-cell-staleApprovals="cellProps">
        <q-td :props="cellProps" :class="cellProps.row.staleApprovals > 0 ? 'bg-orange-1 text-warning' : ''" @click="goToTenant(cellProps.row)">
          {{ cellProps.row.staleApprovals ?? 0 }}
        </q-td>
      </template>
      <template #body-cell-syncFailures="cellProps">
        <q-td :props="cellProps" :class="cellProps.row.syncFailures > 0 ? 'bg-red-1 text-negative' : ''" @click="goToTenant(cellProps.row)">
          {{ cellProps.row.syncFailures ?? 0 }}
        </q-td>
      </template>
      <template #body-cell-repeatedReturns="cellProps">
        <q-td :props="cellProps" :class="cellProps.row.repeatedReturns > 0 ? 'bg-red-1 text-negative' : ''" @click="goToTenant(cellProps.row)">
          {{ cellProps.row.repeatedReturns ?? 0 }}
        </q-td>
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
  title: { type: String, default: "Customer Issues" },
  loading: { type: Boolean, default: false },
  error: { type: [String, null], default: null },
  collapsed: { type: Boolean, default: false },
  customer: { type: [Object, null], default: null }
});
defineEmits(["retry", "update:collapsed"]);

const router = useRouter();
const issues = computed(() => props.customer?.issues || []);

const columns = [
  { name: "tenantName", label: "Tenant", field: "tenantName", align: "left", sortable: true },
  { name: "staleApprovals", label: "Stale Approvals", field: "staleApprovals", align: "center", sortable: true },
  { name: "syncFailures", label: "Sync Failures", field: "syncFailures", align: "center", sortable: true },
  { name: "repeatedReturns", label: "Repeated Returns", field: "repeatedReturns", align: "center", sortable: true }
];

const goToTenant = (row) => {
  if (row?.tenantId) router.push({ path: `/tenants/${row.tenantId}` });
};
</script>
