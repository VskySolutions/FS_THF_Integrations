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
          :options="[
            { label: 'Unassigned', value: 'unassigned' },
            { label: 'Assigned to me', value: 'mine' },
            { label: 'All', value: 'all' }
          ]"
        />
      </template>
    </app-list-header>

    <app-filter-drawer v-model="filterOpen" :chips="allChips" @remove="onRemoveFilter" @clear="onClearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
      <app-text-field v-model="contactFilter" label="Contact (email or mobile)" clearable :dense="false" />
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
      default-sort-by="createdOnUtc"
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
          <q-badge :color="poolStatusColor(cell.row)">{{ poolStatusLabel(cell.row) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-emsFormState="cell">
        <q-td :props="cell">{{ emsStateLabel(cell.row.emsFormState) }}</q-td>
      </template>

      <template #body-cell-clientSubmissionState="cell">
        <q-td :props="cell">{{ submissionStateLabel(cell.row.clientSubmissionState) }}</q-td>
      </template>

      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ fmt.formatDateTime(cell.row.createdOnUtc) }}</q-td>
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
          <q-btn flat round dense color="primary" icon="o_forum" @click="openConversation(cell.row)">
            <q-tooltip>Send message</q-tooltip>
          </q-btn>
          <q-btn
            v-if="cell.row.actions?.canAssign && has(Permissions.RemsPoolRead)"
            flat round dense color="primary"
            :icon="cell.row.assignedAdmin ? 'o_swap_horiz' : 'o_pan_tool'" @click="openAssign(cell.row)"
          >
            <q-tooltip>{{ cell.row.assignedAdmin ? "Assign Admin" : "Pick up or assign" }}</q-tooltip>
          </q-btn>
          <q-btn
            v-if="showEmailLog(cell.row)" flat round dense color="primary" icon="o_mark_email_read"
            :to="'/rems/ems-inbox'"
          >
            <q-tooltip>Email Log</q-tooltip>
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
import AppTextField from "components/common/AppTextField.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import NewRequestDialog from "modules/rems/components/NewRequestDialog.vue";
import AssignAdminDialog from "modules/rems/components/AssignAdminDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
const fmt = useDateFormat();
const {
  priorityLabel, statusLabel, priorityColor, statusColor,
  emsStateLabel, submissionStateLabel, emsDetailAvailable, emsFormActivity
} = useRemsMeta();

// The pool exists to get unclaimed requests picked up, so it opens on those; the other scopes are a click away.
const poolScope = ref("unassigned");

// "Submitted" is the status of everything sitting in the pool, which tells an operator nothing about the
// only thing that matters here: has someone taken it? The backend already draws that line (a request is
// pickable when it is Submitted with no assigned admin), so the pool spells it out. Every other status
// reads as it does elsewhere.
const awaitingPickUp = (row) => row?.status === "submitted" && !row?.assignedAdmin;
const poolStatusLabel = (row) => {
  if (row?.status !== "submitted") return statusLabel(row?.status);
  return awaitingPickUp(row) ? "Waiting For Pickup" : "Picked Up";
};
const poolStatusColor = (row) => (awaitingPickUp(row) ? "amber-8" : statusColor(row?.status));

// The filter carries the pool's wording too. It still filters on the `submitted` status, which is why the
// label names both of the states that status shows up as here rather than just the waiting one.
const POOL_STATUS_OPTIONS = REMS_STATUS_OPTIONS.map((option) =>
  (option.value === "submitted" ? { ...option, label: "Submitted/Waiting For Pickup" } : option));

const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "client", label: "Client / Contact", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "priority", label: "Priority", field: "priority", align: "left", sortable: true, default: true, filterable: false },
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "—", align: "left", default: true, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true, filterable: false },
  { name: "industryGroup", label: "Industry Group", field: (r) => r.industryGroup || "—", align: "left", default: true, filterable: false },
  { name: "customerEmail", label: "Client Email", field: (r) => r.customerEmail || "—", align: "left", default: false, filterable: false },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: POOL_STATUS_OPTIONS },
  { name: "emsFormState", label: "Form Status", field: "emsFormState", align: "left", default: true, filterable: false },
  { name: "clientSubmissionState", label: "Client Submission", field: "clientSubmissionState", align: "left", default: true, filterable: false },
  { name: "createdOnUtc", label: "Created", field: "createdOnUtc", align: "left", sortable: true, default: false, filterable: false },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

// Extra server filter without a display column (the WO-111 list supports a `contact` param).
const contactFilter = ref("");

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.list({
      scope: "pool",
      poolScope: poolScope.value,
      page,
      limit,
      clientName: search.value || undefined,
      contact: contactFilter.value || undefined,
      status: filters.status || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

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
