<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'REMS Partner Dashboard' }]"
      :search="search"
      show-search
      search-placeholder="Search client name"
      show-filters
      :filter-count="filterChips.length"
      :show-add="canCreate"
      add-label="New REMS Request"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @add="openCreate"
      @back="$router.back()"
    />

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
    </app-filter-drawer>

    <app-data-table
      page-key="rems-partner"
      row-key="id"
      title="My REMS requests"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      default-sort-by="createdOnUtc"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-title="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.title }}</div>
          <div class="text-caption text-grey-7">{{ cell.row.clientName || "—" }}</div>
        </q-td>
      </template>

      <template #body-cell-type="cell">
        <q-td :props="cell">{{ typeLabel(cell.row.type) }}</q-td>
      </template>

      <template #body-cell-priority="cell">
        <q-td :props="cell">
          <q-badge :color="priorityColor(cell.row.priority)">{{ priorityLabel(cell.row.priority) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.row.status)">{{ statusLabel(cell.row.status) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ fmt.formatDateTime(cell.row.createdOnUtc) }}</q-td>
      </template>

      <template #body-cell-emsFormState="cell">
        <q-td :props="cell">{{ emsStateLabel(cell.row.emsFormState) }}</q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" :to="detailRoute(cell.row)">
            <q-tooltip>View</q-tooltip>
          </q-btn>
          <q-btn flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 190px;">
                <q-item clickable :to="detailRoute(cell.row)">
                  <q-item-section avatar><q-icon name="o_visibility" /></q-item-section>
                  <q-item-section>View</q-item-section>
                </q-item>
                <q-item v-if="cell.row.actions?.canEdit" clickable @click="openEdit(cell.row)">
                  <q-item-section avatar><q-icon name="o_edit" /></q-item-section>
                  <q-item-section>Edit</q-item-section>
                </q-item>
                <q-item clickable @click="openConversation(cell.row)">
                  <q-item-section avatar><q-icon name="o_forum" /></q-item-section>
                  <q-item-section>Conversations</q-item-section>
                </q-item>
                <q-item v-if="cell.row.actions?.canAssign" clickable @click="openAssign(cell.row)">
                  <q-item-section avatar><q-icon name="o_person_add" /></q-item-section>
                  <q-item-section>Assign Admin</q-item-section>
                </q-item>
                <q-item v-if="cell.row.actions?.canDuplicate" clickable @click="duplicate(cell.row)">
                  <q-item-section avatar><q-icon name="o_content_copy" /></q-item-section>
                  <q-item-section>Duplicate</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_assignment" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No REMS requests yet</div>
          <div class="q-mb-md">Create your first request to start onboarding a client.</div>
          <q-btn v-if="canCreate" unelevated no-caps color="primary" icon="o_add" label="New REMS Request" @click="openCreate" />
        </div>
      </template>
    </app-data-table>

    <new-request-dialog v-model="formOpen" :request-id="editingId" @saved="onSaved" />
    <assign-admin-dialog
      v-model="assignOpen" :request-id="assignRequestId" :current-admin-id="assignCurrentAdminId"
      mode="assign" @assigned="onAssigned"
    />
    <conversation-dialog v-model="conversationOpen" :request-id="conversationId" :subtitle="conversationSubtitle" />
  </q-page>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { debounce } from "quasar";
import { remsApi, getApiErrorMessage } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta, REMS_STATUS_OPTIONS } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import NewRequestDialog from "modules/rems/components/NewRequestDialog.vue";
import AssignAdminDialog from "modules/rems/components/AssignAdminDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
const fmt = useDateFormat();
const { typeLabel, priorityLabel, statusLabel, priorityColor, statusColor, emsStateLabel } = useRemsMeta();

const canCreate = computed(() => has(Permissions.RemsRequestsCreate));

const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "title", label: "Title / Client", field: "title", align: "left", sortable: true, default: true, filterable: false },
  { name: "type", label: "Type", field: "type", align: "left", default: true, filterable: false },
  { name: "priority", label: "Priority", field: "priority", align: "left", sortable: true, default: true, filterable: false },
  { name: "createdOnUtc", label: "Created", field: "createdOnUtc", align: "left", sortable: true, default: true, filterable: false },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: REMS_STATUS_OPTIONS },
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "—", align: "left", default: true, filterable: false },
  { name: "emsFormState", label: "EMS State", field: "emsFormState", align: "left", default: true, filterable: false },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.list({
      scope: "partner",
      page,
      limit,
      clientName: search.value || undefined,
      status: filters.status || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters], reload, { deep: true });

const detailRoute = (row) => ({ name: "rems_request_detail", params: { id: row.id } });

// ---- Create / Edit ----
const formOpen = ref(false);
const editingId = ref(null);
const openCreate = () => { editingId.value = null; formOpen.value = true; };
const openEdit = (row) => { editingId.value = row.id; formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

// ---- Assign ----
const assignOpen = ref(false);
const assignRequestId = ref(null);
const assignCurrentAdminId = ref(null);
const openAssign = (row) => {
  assignRequestId.value = row.id;
  assignCurrentAdminId.value = row.assignedAdmin?.id || null;
  assignOpen.value = true;
};
const onAssigned = () => { assignOpen.value = false; load(); };

// ---- Conversation ----
const conversationOpen = ref(false);
const conversationId = ref(null);
const conversationSubtitle = ref("");
const openConversation = (row) => {
  conversationId.value = row.id;
  conversationSubtitle.value = `${row.remsNumber} — ${row.title}`;
  conversationOpen.value = true;
};

// ---- Duplicate ----
const duplicate = async (row) => {
  const ok = await confirm({
    title: "Duplicate request",
    message: `Create a new draft copy of ${row.remsNumber} — "${row.title}"?`,
    confirmLabel: "Duplicate"
  });
  if (!ok) return;
  try {
    await remsApi.duplicate(row.id);
    notify.success("Request duplicated as a new draft.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
</script>
