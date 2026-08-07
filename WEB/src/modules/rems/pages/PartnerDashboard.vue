<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'REMS Partner Dashboard' }]"
      :search="search"
      show-search
      search-placeholder="Search client name"
      show-filters
      :filter-count="allChips.length"
      :show-add="canCreate"
      add-label="New REMS Request"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @add="openCreate"
      @back="$router.back()"
    />

    <app-filter-drawer v-model="filterOpen" :chips="allChips" @remove="onRemoveFilter" @clear="onClearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
      <!-- Server filters with no column of their own: the contact line is captioned under the title
           rather than shown as a column, and a created range is two controls, not one.
           Each sits as its own full-width slot child, like every control above it — AppFilterDrawer
           already spaces and aligns what it is given, so no row wrapper or margin class is wanted here. -->
      <app-text-field v-model="extras.contact" label="Contact (email or mobile)" clearable :dense="false" />
      <app-date-field v-model="extras.createdFrom" label="Created From" :dense="false" />
      <app-date-field v-model="extras.createdTo" label="Created To" :dense="false" />
      <div v-if="invalidRange" class="text-caption text-negative">
        “Created From” is after “Created To” — no request can match both.
      </div>
      <q-toggle
        v-if="canManageDeleted" v-model="showDeleted" label="Show deleted?" dense class="q-mt-md"
      />
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
      default-sort-by="updatedOnUtc"
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
          <q-badge :color="requestStatusColor(cell.row)">{{ requestStatusLabel(cell.row) }}</q-badge>
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
          <q-btn
            v-if="cell.row.actions?.canEdit && cell.row.status === 'draft'"
            flat round dense color="primary" icon="o_edit" @click="openEdit(cell.row)"
          >
            <q-tooltip>Edit</q-tooltip>
          </q-btn>
          <!-- Hand a draft straight to a named Admin instead of letting it go to the pool unclaimed.
               Only offered on drafts: once submitted, re-assignment is the Admin Pool's job. -->
          <q-btn
            v-if="cell.row.actions?.canAssign && cell.row.status === 'draft'"
            flat round dense color="primary" icon="o_person_add" @click="openAssign(cell.row)"
          >
            <q-tooltip>Assign to Admin</q-tooltip>
          </q-btn>
          <q-btn
            v-if="cell.row.actions?.canDuplicate"
            flat round dense color="primary" icon="o_content_copy" @click="duplicate(cell.row)"
          >
            <q-tooltip>Duplicate</q-tooltip>
          </q-btn>
          <q-btn flat round dense color="primary" icon="o_forum" @click="openConversation(cell.row)">
            <q-tooltip>Notes</q-tooltip>
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

    <deleted-records-panel
      v-if="canManageDeleted" :entity-type="EntityType.Rems" :show="showDeleted" @restored="load"
    />

    <new-request-dialog v-model="formOpen" :request-id="editingId" @saved="onSaved" />
    <assign-admin-dialog
      v-model="assignOpen" mode="draft" :request-id="assignTarget.id"
      :request-number="assignTarget.number" :request-title="assignTarget.title"
      @assigned="onAssigned"
    />
    <conversation-dialog v-model="conversationOpen" :request-id="conversationId" :subtitle="conversationSubtitle" />
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from "vue";
import { debounce } from "quasar";
import { remsApi, getApiErrorMessage, EntityType } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDeletedRecords } from "composables/useDeletedRecords";
import { useDateFormat } from "composables/useDateFormat";
import { useAuditColumns } from "composables/useAuditColumns";
import { useRemsMeta } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppTextField from "components/common/AppTextField.vue";
import AppDateField from "components/common/AppDateField.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import DeletedRecordsPanel from "components/universal/DeletedRecordsPanel.vue";
import NewRequestDialog from "modules/rems/components/NewRequestDialog.vue";
import AssignAdminDialog from "modules/rems/components/AssignAdminDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";

const { showDeleted, canManageDeleted } = useDeletedRecords();
const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
const fmt = useDateFormat();
const auditColumns = useAuditColumns();
const {
  typeLabel, priorityLabel, priorityColor, requestStatusLabel, requestStatusColor,
  emsStateLabel, submissionStateLabel,
  statusFilterOptions, typeOptions, priorityOptions
} = useRemsMeta();

const canCreate = computed(() => has(Permissions.RemsRequestsCreate));

// The Assigned Admin filter needs the admin list, which is gated the same way the assign action is.
const canSeeAdmins = computed(() => has(Permissions.RemsRequestsAssign) || has(Permissions.RemsPoolRead));
const adminFilterOptions = ref([]);
onMounted(async () => {
  if (!canSeeAdmins.value) return;
  try {
    const admins = await remsApi.admins();
    adminFilterOptions.value = (admins || []).map((a) => ({ label: a.name, value: a.id }));
  } catch {
    // A filter nobody can populate simply stays empty; the list itself is unaffected, and the table's
    // own error toast is the one worth showing.
  }
});

const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "title", label: "Title / Client", field: "title", align: "left", sortable: true, default: true, filterable: false },
  { name: "type", label: "Type", field: "type", align: "left", default: true, filterOptions: typeOptions.value },
  { name: "priority", label: "Priority", field: "priority", align: "left", sortable: true, default: true, filterOptions: priorityOptions.value },
  // Filtered through the Created From/To pair in the drawer — a single "contains" box over a formatted
  // timestamp would match on the digits of a date rather than on the date.
  { name: "createdOnUtc", label: "Created", field: "createdOnUtc", align: "left", sortable: true, default: true, filterable: false },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: statusFilterOptions.value },
  // Only offered to callers who may read the admin list; without it the picker would be empty, which
  // reads as "nobody is assigned" rather than "you cannot see who is".
  {
    name: "assignedAdmin",
    label: "Assigned Admin",
    field: (r) => r.assignedAdmin?.name || "—",
    align: "left",
    default: true,
    ...(canSeeAdmins.value ? { filterOptions: adminFilterOptions.value } : { filterable: false })
  },
  { name: "emsFormState", label: "EMS State", field: "emsFormState", align: "left", default: true, filterable: false },
  // Off by default (the title cell already captions the client), but every field the row carries is
  // offered in the Columns menu rather than being unreachable.
  { name: "clientName", label: "Client", field: "clientName", align: "left", sortable: true, default: false, filterable: false },
  { name: "customerEmail", label: "Client Email", field: (r) => r.customerEmail || "—", align: "left", default: false, filterable: false },
  { name: "customerMobileNumber", label: "Client Mobile", field: (r) => r.customerMobileNumber || "—", align: "left", default: false, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: false, filterable: false },
  { name: "industryGroup", label: "Industry Group", field: (r) => r.industryGroup || "—", align: "left", default: false, filterable: false },
  { name: "clientSubmissionState", label: "Client Submission", field: (r) => submissionStateLabel(r.clientSubmissionState), align: "left", default: false, filterable: false },
  // Created On is already a visible column here, so only the other three come from the shared set —
  // two columns of the same name would collide in AppDataTable's visibility map.
  ...auditColumns({ only: ["createdBy", "updatedBy", "updatedOnUtc"] }),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

// Server filters with no column to hang off. Kept in one reactive object so the chips, the reset and
// the watcher below each have a single thing to read.
const extras = reactive({ contact: "", createdFrom: "", createdTo: "" });

const invalidRange = computed(() =>
  !!extras.createdFrom && !!extras.createdTo && extras.createdFrom > extras.createdTo);

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.list({
      scope: "partner",
      page,
      limit,
      clientName: search.value || undefined,
      status: filters.status || undefined,
      type: filters.type || undefined,
      priority: filters.priority || undefined,
      assignedAdminUserId: filters.assignedAdmin || undefined,
      contact: extras.contact || undefined,
      // The pickers are date-only and read in the tenant's zone; the column they filter is a UTC
      // instant. Both ends are converted to that day's real boundaries, so "to" includes its own day.
      createdFrom: fmt.zonedDayBoundaryUtc(extras.createdFrom, "start"),
      createdTo: fmt.zonedDayBoundaryUtc(extras.createdTo, "end")
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });

// The column chips plus one per standalone filter, so everything narrowing the list is visible in the
// same place and removable the same way.
const extraChips = [
  { key: "contact", label: "Contact" },
  { key: "createdFrom", label: "Created From" },
  { key: "createdTo", label: "Created To" }
];
const allChips = computed(() => [
  ...filterChips.value,
  ...extraChips.filter((c) => extras[c.key]).map((c) => ({ key: c.key, label: `${c.label}: ${extras[c.key]}` }))
]);
const onRemoveFilter = (key) => {
  if (key in extras) extras[key] = "";
  else removeFilter(key);
};
const onClearFilters = () => {
  clearFilters();
  extraChips.forEach((c) => { extras[c.key] = ""; });
};

const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters, extras], reload, { deep: true });

const detailRoute = (row) => ({ name: "rems_request_detail", params: { id: row.id } });

// ---- Create / Edit ----
const formOpen = ref(false);
const editingId = ref(null);
const openCreate = () => { editingId.value = null; formOpen.value = true; };
const openEdit = (row) => { editingId.value = row.id; formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

// ---- Assign a draft straight to an Admin (submits it in the same step) ----
const assignOpen = ref(false);
const assignTarget = reactive({ id: null, number: "", title: "" });
const openAssign = (row) => {
  assignTarget.id = row.id;
  assignTarget.number = row.remsNumber || "";
  assignTarget.title = row.title || "";
  assignOpen.value = true;
};
// The row leaves draft on success, so the list has to be re-read rather than patched in place.
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
