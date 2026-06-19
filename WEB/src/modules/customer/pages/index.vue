<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Customers' }]"
      :search="search"
      show-search
      search-placeholder="Search company, legal name or number"
      show-filters
      :filter-count="filterChips.length"
      :show-add="canDataEntry"
      add-label="Add Customer"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @add="openCreate"
      @back="$router.back()"
    >
      <template #actions>
        <app-select
          v-if="canChooseTenant" v-model="selectedTenantId" :options="tenantOptions" label="Tenant"
          :loading="loadingTenants" :clearable="true" style="min-width: 220px;"
        />
      </template>
    </app-list-header>

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
    </app-filter-drawer>

    <app-data-table
      page-key="customers"
      row-key="id"
      title="All customers"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      default-sort-by="createdOnUtc"
      selectable
      @request="onRequest"
      @refresh="load"
      @update:selected="selected = $event"
    >
      <!-- Bulk actions are status-aware: only shown when the selection has eligible rows. -->
      <template v-if="canDataEntry" #bulk-actions="{ selected: sel }">
        <q-btn v-if="sel.some(canSubmit)" flat dense no-caps color="primary" icon="o_send" label="Submit for Approval" @click="bulkSubmit(sel)" />
        <q-btn v-if="sel.some(canDelete)" flat dense no-caps color="negative" icon="o_delete" label="Delete" @click="bulkDelete(sel)" />
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.value)">{{ statusLabel(cell.value) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" :to="{ name: 'customer_detail', params: { id: cell.row.id } }">
            <q-tooltip>View / Manage</q-tooltip>
          </q-btn>
          <q-btn v-if="canEdit(cell.row) || canDelete(cell.row)" flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 170px;">
                <q-item clickable :to="{ name: 'customer_detail', params: { id: cell.row.id } }">
                  <q-item-section avatar><q-icon name="o_visibility" /></q-item-section>
                  <q-item-section>View</q-item-section>
                </q-item>
                <q-item v-if="canEdit(cell.row)" clickable :to="{ name: 'customer_detail', params: { id: cell.row.id } }">
                  <q-item-section avatar><q-icon name="o_edit" /></q-item-section>
                  <q-item-section>Edit</q-item-section>
                </q-item>
                <q-separator v-if="canDelete(cell.row)" />
                <q-item v-if="canDelete(cell.row)" clickable @click="removeCustomer(cell.row)">
                  <q-item-section avatar><q-icon name="o_delete" color="negative" /></q-item-section>
                  <q-item-section class="text-negative">Delete</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_groups" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No customers yet</div>
          <div class="q-mb-md">Add your first customer to begin the onboarding workflow.</div>
          <q-btn unelevated no-caps color="primary" icon="o_add" label="Add Customer" @click="openCreate" />
        </div>
      </template>
    </app-data-table>

    <customer-form-drawer ref="createDrawer" v-model="formOpen" :tenant-id="selectedTenantId" @saved="onSaved" />
  </q-page>
</template>

<script setup>
import { ref, computed, watch, onMounted } from "vue";
import { debounce } from "quasar";
import { customerApi, getApiErrorMessage } from "services/api";
import { useTenantStore } from "stores/tenant";
import { useTenantOptions } from "composables/useTenantOptions";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";
import { useCustomerStatus } from "composables/useCustomerStatus";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import CustomerFormDrawer from "modules/customer/components/CustomerFormDrawer.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const tenantStore = useTenantStore();
const { canChooseTenant, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();
const { has } = usePermissions();
// Customer data-entry capability gates create / submit / delete (the data-entry stage of the workflow).
const canDataEntry = computed(() => has(Permissions.CustomersDataEntry));
const { customerStatusColor: statusColor, customerStatusLabel: statusLabel } = useCustomerStatus();

// Super admins scope the list to a chosen tenant via the dropdown; the value is the tenantId sent
// to the API. Others are auto-scoped server-side to their active tenant.
const selectedTenantId = ref(null);

const STATUS_OPTIONS = [
  "Draft", "Submitted", "UnderReview", "PendingApproval", "PartiallyApproved",
  "Approved", "SyncInProgress", "Synced", "Rejected", "Returned", "Failed"
].map((s) => ({ label: statusLabel(s), value: s }));

const columns = computed(() => [
  { name: "customerRequestNumber", label: "Customer Request Number", field: "customerRequestNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "companyName", label: "Company Name", field: "companyName", align: "left", sortable: true, default: true, filterable: false },
  { name: "legalName", label: "Legal Name", field: "legalName", align: "left", sortable: true, default: true, filterable: false },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: STATUS_OPTIONS },
  ...(canChooseTenant.value ? [{ name: "tenantName", label: "Tenant", field: "tenantName", align: "left", sortable: true, default: true, filterable: false }] : []),
  { name: "createdOnUtc", label: "Submitted Date", field: (r) => fmt.formatDateTime(r.createdOnUtc), align: "left", sortable: true, default: true, filterable: false },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const { rows, loading, totalRecords, selected, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    customerApi.list({
      page,
      limit,
      search: search.value || undefined,
      status: filters.status || undefined,
      tenantId: (canChooseTenant.value && selectedTenantId.value) ? selectedTenantId.value : undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters], reload, { deep: true });
watch(selectedTenantId, () => { pagination.value.page = 1; load(); });

onMounted(() => {
  if (canChooseTenant.value) {
    loadTenants();
    selectedTenantId.value = tenantStore.activeTenantId;
  }
});

// Draft and Returned customers are editable/submittable; only Draft customers may be deleted.
const canEdit = (row) => row.status === "Draft" || row.status === "Returned";
const canSubmit = (row) => canDataEntry.value && (row.status === "Draft" || row.status === "Returned");
const canDelete = (row) => canDataEntry.value && row.status === "Draft";

// ---- Create ----
const formOpen = ref(false);
const openCreate = () => { formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

const removeCustomer = async (row) => {
  const ok = await confirm({
    title: "Delete customer",
    message: `Delete draft "${row.companyName || row.customerRequestNumber}"? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await customerApi.remove(row.id);
    notify.success("Customer deleted.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// Bulk submit for approval from the multi-select toolbar. Only Draft/Returned customers are
// submittable; records flagged as potential duplicates are left unsubmitted for individual review.
const bulkSubmit = async (sel) => {
  const submittable = sel.filter((r) => canSubmit(r));
  const skipped = sel.length - submittable.length;
  if (!submittable.length) {
    notify.error("Only Draft or Returned customers can be submitted for approval.");
    return;
  }
  const ok = await confirm({
    title: "Submit for approval",
    message: `Submit ${submittable.length} customer(s) for approval?${skipped ? ` (${skipped} not in a submittable state will be skipped.)` : ""}`,
    confirmLabel: "Submit"
  });
  if (!ok) return;
  try {
    const results = await Promise.all(submittable.map((r) => customerApi.submit(r.id, false)));
    const submittedCount = results.filter((x) => x?.submitted).length;
    const dupCount = results.length - submittedCount;
    selected.value = [];
    load();
    if (dupCount) {
      notify.warning(`${submittedCount} submitted. ${dupCount} flagged as possible duplicate(s) — open them individually to review and confirm.`);
    } else {
      notify.success(`${submittedCount} customer(s) submitted for approval.`);
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// Bulk delete from the multi-select toolbar. Only Draft customers are deletable; non-drafts are skipped.
const bulkDelete = async (sel) => {
  const deletable = sel.filter((r) => canDelete(r));
  const skipped = sel.length - deletable.length;
  if (!deletable.length) {
    notify.error("Only Draft customers can be deleted.");
    return;
  }
  const ok = await confirm({
    title: "Delete customers",
    message: `Delete ${deletable.length} draft customer(s)?${skipped ? ` (${skipped} non-draft will be skipped.)` : ""} This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await Promise.all(deletable.map((r) => customerApi.remove(r.id)));
    notify.success("Customers deleted.");
    selected.value = [];
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
</script>
