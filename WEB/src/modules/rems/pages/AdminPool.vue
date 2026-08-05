<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'REMS Admin Pool' }]"
      :search="search"
      show-search
      search-placeholder="Search client name"
      show-filters
      :filter-count="allChips.length"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @back="$router.back()"
    >
      <template #actions>
        <q-btn-toggle
          v-model="poolScope"
          no-caps unelevated dense
          toggle-color="primary" color="white" text-color="primary"
          :options="scopeOptions"
        />
      </template>
    </app-list-header>

    <app-filter-drawer v-model="filterOpen" :chips="allChips" @remove="onRemoveFilter" @clear="onClearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
      <app-text-field v-model="contactFilter" label="Contact (email or mobile)" clearable :dense="false" />
      <q-toggle
        v-if="canManageDeleted" v-model="showDeleted" label="Show deleted?" dense class="q-mt-md"
      />
    </app-filter-drawer>

    <app-data-table
      page-key="rems-admin-pool"
      row-key="id"
      title="Admin Pool"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      default-sort-by="updatedOnUtc"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-remsNumber="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.remsNumber || "—" }}</div>
        </q-td>
      </template>

      <template #body-cell-client="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.clientName || "—" }}</div>
          <div class="text-caption text-grey-7">{{ cell.row.customerEmail || cell.row.customerMobileNumber || "—" }}</div>
        </q-td>
      </template>

      <template #body-cell-priority="cell">
        <q-td :props="cell">
          <q-badge :color="priorityColor(cell.row.priority)">{{ priorityLabel(cell.row.priority) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="requestStatusColor(cell.row)">{{ requestStatusLabel(cell.row) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-emsFormState="cell">
        <q-td :props="cell">{{ emsStateLabel(cell.row.emsFormState) }}</q-td>
      </template>

      <template #body-cell-clientSubmissionState="cell">
        <q-td :props="cell">{{ submissionStateLabel(cell.row.clientSubmissionState) }}</q-td>
      </template>

      <!-- Action order is the same on every REMS list: View, Edit, this list's own actions, Notes, Delete.
           Notes and Delete keep the last two seats everywhere, so the button under the cursor never changes
           meaning as you move between lists — and Delete is never adjacent to something routine. -->
      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" :to="detailRoute(cell.row)">
            <q-tooltip>View</q-tooltip>
          </q-btn>
          <q-btn
            v-if="cell.row.actions?.canEdit && cell.row.status === 'draft'"
            flat round dense color="primary" icon="o_edit" @click="openEdit(cell.row)"
          >
            <q-tooltip>Edit</q-tooltip>
          </q-btn>
          <q-btn
            v-if="cell.row.actions?.canAssign && has(Permissions.RemsPoolRead)"
            flat round dense color="primary"
            :icon="cell.row.assignedAdmin ? 'o_swap_horiz' : 'o_pan_tool'" @click="openAssign(cell.row)"
          >
            <q-tooltip>{{ cell.row.assignedAdmin ? "Assign Admin" : "Pick up or assign" }}</q-tooltip>
          </q-btn>
          <q-btn
            v-if="showEngagement(cell.row)" flat round dense color="primary" icon="o_work"
            :disable="!emsDetailAvailable(cell.row)"
            :to="emsDetailAvailable(cell.row) ? `/rems/engagements/${cell.row.id}` : undefined"
          >
            <q-tooltip>
              {{ emsDetailAvailable(cell.row) ? "Engagement Setup" : "Available once the customer submits their form" }}
            </q-tooltip>
          </q-btn>
          <q-btn
            v-if="showEmailLog(cell.row)" flat round dense color="primary" icon="o_mark_email_read"
            :to="'/rems/ems-inbox'"
          >
            <q-tooltip>Email Log</q-tooltip>
          </q-btn>
          <q-btn flat round dense color="primary" icon="o_forum" @click="openConversation(cell.row)">
            <q-tooltip>Notes</q-tooltip>
          </q-btn>
          <q-btn
            v-if="cell.row.actions?.canDelete" flat round dense color="negative" icon="o_delete"
            @click="removeRequest(cell.row)"
          >
            <q-tooltip>Delete</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_inbox" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">The pool is empty</div>
          <div>No requests match this view. Try switching the scope or clearing filters.</div>
        </div>
      </template>
    </app-data-table>

    <deleted-records-panel
      v-if="canManageDeleted" :entity-type="EntityType.Rems" :show="showDeleted" @restored="load"
    />

    <new-request-dialog v-model="formOpen" :request-id="editingId" @saved="onSaved" />
    <assign-admin-dialog
      v-model="assignOpen" :request-id="assignRequestId" :current-admin-id="assignCurrentAdminId"
      :mode="assignMode" @assigned="onAssigned"
    />
    <conversation-dialog v-model="conversationOpen" :request-id="conversationId" :subtitle="conversationSubtitle" />
  </q-page>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { debounce, LocalStorage } from "quasar";
import { remsApi, getApiErrorMessage, EntityType } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDeletedRecords } from "composables/useDeletedRecords";
import { useAuditColumns } from "composables/useAuditColumns";
import { useRemsMeta } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import DeletedRecordsPanel from "components/universal/DeletedRecordsPanel.vue";
import NewRequestDialog from "modules/rems/components/NewRequestDialog.vue";
import AssignAdminDialog from "modules/rems/components/AssignAdminDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";

const { showDeleted, canManageDeleted } = useDeletedRecords();
const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
// Date formatting now lives inside the shared audit columns; the pool has no other date cell of its own.
const auditColumns = useAuditColumns();
const {
  typeLabel, priorityLabel, priorityColor, requestStatusLabel, requestStatusColor,
  emsStateLabel, submissionStateLabel, emsDetailAvailable, emsFormActivity,
  statusFilterOptions
} = useRemsMeta();

const POOL_SCOPE_OPTIONS = [
  { label: "Unassigned", value: "unassigned" },
  { label: "Assigned to me", value: "mine" },
  { label: "All", value: "all" }
];

// Which view the user last worked in, remembered across visits the same way the engagement workspace
// remembers its splitter — the pool is somewhere people sit for a shift, and resetting the scope on
// every refresh loses their place. Unassigned is still the default on a first visit: the pool exists to
// get unclaimed requests picked up. A stored value is checked against the options, so a stale or
// hand-edited key falls back to the default instead of sending an unknown scope to the API.
const POOL_SCOPE_KEY = "remsPoolScope";
const storedScope = LocalStorage.getItem(POOL_SCOPE_KEY);
const poolScope = ref(
  POOL_SCOPE_OPTIONS.some((o) => o.value === storedScope) ? storedScope : "unassigned");
watch(poolScope, (value) => LocalStorage.set(POOL_SCOPE_KEY, value));

// How much work sits behind each view, so the size of the queue is visible without clicking through.
// Counts honour the active filters exactly as the list does, so a badge can never promise rows the view
// would not show. A count is shown only when there is something to show: zero reads better as a plain
// label than as "(0)", and it means a number on a view always signals work waiting. The same falsy test
// covers the not-yet-loaded case, so the toggle never flashes "(0)" before the first response lands.
const poolCounts = ref(null);
const scopeOptions = computed(() => POOL_SCOPE_OPTIONS.map((option) => {
  const count = poolCounts.value?.[option.value];
  return count ? { ...option, label: `${option.label} (${count})` } : option;
}));

const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "client", label: "Client / Contact", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "title", label: "Title", field: "title", align: "left", sortable: true, default: false, filterable: false },
  { name: "type", label: "Type", field: (r) => typeLabel(r.type), align: "left", default: false, filterable: false },
  { name: "priority", label: "Priority", field: "priority", align: "left", sortable: true, default: true, filterable: false },
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "—", align: "left", default: true, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true, filterable: false },
  { name: "industryGroup", label: "Industry Group", field: (r) => r.industryGroup || "—", align: "left", default: true, filterable: false },
  { name: "customerEmail", label: "Client Email", field: (r) => r.customerEmail || "—", align: "left", default: false, filterable: false },
  { name: "customerMobileNumber", label: "Client Mobile", field: (r) => r.customerMobileNumber || "—", align: "left", default: false, filterable: false },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: statusFilterOptions.value },
  { name: "emsFormState", label: "Form Status", field: "emsFormState", align: "left", default: true, filterable: false },
  { name: "clientSubmissionState", label: "Client Submission", field: "clientSubmissionState", align: "left", default: true, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

// Extra server filter without a display column (the WO-111 list supports a `contact` param).
const contactFilter = ref("");

// The filters both the list and the counts are narrowed by — one definition, so the badge on a view and
// the rows behind it can never be computed from different criteria.
const activeFilters = () => ({
  clientName: search.value || undefined,
  contact: contactFilter.value || undefined,
  status: filters.status || undefined
});

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.list({ scope: "pool", poolScope: poolScope.value, page, limit, ...activeFilters() })
      .then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// A failed count is swallowed: a stale badge is not worth a second error toast beside the list's own.
const loadCounts = async () => {
  try {
    poolCounts.value = await remsApi.poolCounts(activeFilters());
  } catch {
    // keep whatever was showing rather than blanking the toggle
  }
};

// Counts follow the list. Everything that refreshes the table goes through `load` — mount, a filter or
// scope change, an assignment, a delete, the Refresh button, a tenant switch — so keying off the loaded
// rows keeps the badges in step without every one of those call sites having to remember them. Paging
// re-counts needlessly; that is one small aggregate query, cheaper than three refresh paths to maintain.
watch(rows, loadCounts);

const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });

// Combine the column-filter chips with the standalone contact filter.
const allChips = computed(() => {
  const chips = [...filterChips.value];
  if (contactFilter.value) chips.push({ key: "contact", label: `Contact: ${contactFilter.value}` });
  return chips;
});
const onRemoveFilter = (key) => { if (key === "contact") contactFilter.value = ""; else removeFilter(key); };
const onClearFilters = () => { clearFilters(); contactFilter.value = ""; };

const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters, contactFilter, poolScope], reload, { deep: true });

const detailRoute = (row) => ({ name: "rems_request_detail", params: { id: row.id } });

// EMS-inbox / engagement hand-offs live in later WOs; gate them by permission + form/submission state.
const showEmailLog = (row) => has(Permissions.RemsEmailLogRead) && emsFormActivity(row);
const showEngagement = (row) => has(Permissions.RemsEngagementsManage);

// ---- Edit ----
const formOpen = ref(false);
const editingId = ref(null);
const openEdit = (row) => { editingId.value = row.id; formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

// ---- Assign / Pick Up ----
const assignOpen = ref(false);
const assignRequestId = ref(null);
const assignCurrentAdminId = ref(null);
const assignMode = ref("assign");
const openAssign = (row) => {
  assignRequestId.value = row.id;
  assignCurrentAdminId.value = row.assignedAdmin?.id || null;
  assignMode.value = row.assignedAdmin ? "assign" : "pickup";
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

// ---- Delete ----
const removeRequest = async (row) => {
  const ok = await confirm({
    title: "Delete REMS request",
    message: `Delete ${row.remsNumber} for "${row.clientName}"? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await remsApi.remove(row.id);
    notify.success("REMS request deleted.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
</script>
