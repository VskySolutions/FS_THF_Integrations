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
      <!-- Admin-only "Show deleted" toggle (records.adminDelete); reveals the deleted-records table below. -->
      <template v-if="canAdminDelete">
        <q-toggle v-model="showDeleted" label="Show deleted records" dense class="q-mb-sm" />
        <q-separator class="q-mb-md" />
      </template>
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
      :pinned-row-keys="pinnedKeys"
      @request="onRequest"
      @refresh="load"
      @update:selected="selected = $event"
    >
      <!-- Bulk actions are status-aware: only shown when the selection has eligible rows. -->
      <template v-if="canDataEntry" #bulk-actions="{ selected: sel }">
        <q-btn v-if="sel.some(canSubmit)" flat dense no-caps color="primary" icon="o_send" label="Submit for Approval" @click="bulkSubmit(sel)" />
        <q-btn v-if="sel.some(canDelete)" flat dense no-caps color="negative" icon="o_delete" label="Delete" @click="bulkDelete(sel)" />
      </template>

      <!-- Universal Features colour shown as a bar at the very left edge of the row (before the checkbox),
           plus a pin badge beside it when the record is pinned. -->
      <template #body-selection="scope">
        <div class="uf-row-accent" :style="{ backgroundColor: colourMap[scope.row.id] || 'transparent' }" />
        <q-checkbox v-model="scope.selected" dense />
        <q-icon v-if="pinnedSet.has(scope.row.id)" name="o_push_pin" size="16px" color="primary" class="q-ml-xs">
          <q-tooltip>Pinned</q-tooltip>
        </q-icon>
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
          <!-- "More" menu: page actions (View/Edit/Delete) above the universal feature actions. -->
          <entity-row-actions-menu
            :entity-type="EntityType.CustomerRequest"
            :entity-id="cell.row.id"
            :label="cell.row.customerRequestNumber || 'customer'"
            :initial-pinned="pinnedSet.has(cell.row.id)"
            :pin-disabled="pinLimitReached"
            :pin-limit="PIN_LIMIT"
            @colour-change="(colour) => onRowColourChange(cell.row.id, colour)"
            @pin-change="(isPinned) => onRowPinChange(cell.row.id, isPinned)"
          >
            <q-item v-close-popup clickable :to="{ name: 'customer_detail', params: { id: cell.row.id } }">
              <q-item-section avatar><q-icon name="o_visibility" /></q-item-section>
              <q-item-section>View</q-item-section>
            </q-item>
            <q-item v-if="canEdit(cell.row)" v-close-popup clickable :to="{ name: 'customer_detail', params: { id: cell.row.id } }">
              <q-item-section avatar><q-icon name="o_edit" /></q-item-section>
              <q-item-section>Edit</q-item-section>
            </q-item>
            <q-item v-if="canDelete(cell.row)" v-close-popup clickable @click="removeCustomer(cell.row)">
              <q-item-section avatar><q-icon name="o_delete" color="negative" /></q-item-section>
              <q-item-section class="text-negative">Delete</q-item-section>
            </q-item>
          </entity-row-actions-menu>
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

    <!-- Deleted records (restore, permanently delete) — revealed by the "Show deleted" filter toggle. -->
    <deleted-records-panel
      v-if="canAdminDelete"
      :entity-type="EntityType.CustomerRequest"
      :show="showDeleted"
      class="q-mt-md"
      @restored="load"
    />

    <customer-form-drawer ref="createDrawer" v-model="formOpen" :tenant-id="selectedTenantId" @saved="onSaved" />
  </q-page>
</template>

<script setup>
import { ref, computed, watch, onMounted } from "vue";
import { debounce } from "quasar";
import { customerApi, ufColourApi, ufPinApi, getApiErrorMessage } from "services/api";
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
import DeletedRecordsPanel from "components/universal/DeletedRecordsPanel.vue";
import EntityRowActionsMenu from "components/universal/EntityRowActionsMenu.vue";
import { EntityType } from "services/api";

const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const tenantStore = useTenantStore();
const { canChooseTenant, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();
const { has } = usePermissions();
const canAdminDelete = computed(() => has(Permissions.RecordsAdminDelete));
// Bound to the "Show deleted" toggle inside the filter drawer (admin-only).
const showDeleted = ref(false);
// Customer data-entry capability gates create / submit / delete (the data-entry stage of the workflow).
const canDataEntry = computed(() => has(Permissions.CustomersDataEntry));
const { customerStatusColor: statusColor, customerStatusLabel: statusLabel } = useCustomerStatus();

// Super admins scope the list to a chosen tenant via the dropdown; the value is the tenantId sent
// to the API. Others are auto-scoped server-side to their active tenant.
const selectedTenantId = ref(null);

const STATUS_OPTIONS = [
  "Draft", "Submitted", "UnderReview", "PendingApproval", "PartiallyApproved",
  "Approved", "Rejected", "Returned"
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

// Universal Features colour codes for the visible rows → a left-border accent on each coloured row.
const colourMap = ref({});
const loadColours = async () => {
  const ids = rows.value.map((r) => r.id);
  if (!ids.length) { colourMap.value = {}; return; }
  try {
    colourMap.value = (await ufColourApi.batch(EntityType.CustomerRequest, ids)) || {};
  } catch { /* non-critical */ }
};
watch(rows, loadColours);

const onRowColourChange = (id, colour) => { colourMap.value = { ...colourMap.value, [id]: colour }; };

// The user's pinned customer records → a pin badge on those rows. Pins are a small per-user set,
// loaded once; the row menu keeps it in sync via @pin-change.
const pinnedSet = ref(new Set());
const loadPins = async () => {
  try {
    const res = await ufPinApi.list({ page: 1, limit: 50 });
    pinnedSet.value = new Set(
      (res?.data || [])
        .filter((p) => Number(p.entityType) === Number(EntityType.CustomerRequest))
        .map((p) => p.entityId)
    );
  } catch { /* non-critical */ }
};
const onRowPinChange = (id, isPinned) => {
  const next = new Set(pinnedSet.value);
  if (isPinned) next.add(id); else next.delete(id);
  pinnedSet.value = next;
};
// Pinned customers float to the top of the table; capped at 5 per the backend pin limit.
const PIN_LIMIT = 5;
const pinnedKeys = computed(() => [...pinnedSet.value]);
const pinLimitReached = computed(() => pinnedSet.value.size >= PIN_LIMIT);

onMounted(() => {
  if (canChooseTenant.value) {
    loadTenants();
    selectedTenantId.value = tenantStore.activeTenantId;
  }
  loadPins();
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

<style scoped>
/* Anchor the colour accent to the selection cell so it sits at the row's left edge. */
:deep(.app-data-table.with-selection tbody td:first-child) {
  position: relative;
}
/* The Universal Features colour bar, drawn before the checkbox. */
.uf-row-accent {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
}
</style>
