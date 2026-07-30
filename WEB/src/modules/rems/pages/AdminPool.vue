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
          <q-badge :color="statusColor(cell.row.status)">{{ statusLabel(cell.row.status) }}</q-badge>
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
          <q-btn flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 210px;">
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
                  <q-item-section>Send message</q-item-section>
                </q-item>
                <q-item v-if="cell.row.actions?.canAssign" clickable @click="openAssign(cell.row)">
                  <q-item-section avatar><q-icon :name="cell.row.assignedAdmin ? 'o_swap_horiz' : 'o_pan_tool'" /></q-item-section>
                  <q-item-section>{{ cell.row.assignedAdmin ? "Assign Admin" : "Pick Up" }}</q-item-section>
                </q-item>

                <template v-if="showEmailLog(cell.row) || showEngagement(cell.row)">
                  <q-separator />
                  <q-item v-if="showEmailLog(cell.row)" clickable :to="'/rems/ems-inbox'">
                    <q-item-section avatar><q-icon name="o_mark_email_read" /></q-item-section>
                    <q-item-section>Email Log</q-item-section>
                  </q-item>
                  <q-item
                    v-if="showEngagement(cell.row)" clickable
                    :disable="!emsDetailAvailable(cell.row)"
                    :to="emsDetailAvailable(cell.row) ? `/rems/engagements/${cell.row.id}` : undefined"
                  >
                    <q-item-section avatar><q-icon name="o_engineering" /></q-item-section>
                    <q-item-section>
                      Engagement Setup
                      <q-tooltip v-if="!emsDetailAvailable(cell.row)">Available once the customer submits their form</q-tooltip>
                    </q-item-section>
                  </q-item>
                </template>

                <template v-if="cell.row.actions?.canDelete">
                  <q-separator />
                  <q-item clickable @click="removeRequest(cell.row)">
                    <q-item-section avatar><q-icon name="o_delete" color="negative" /></q-item-section>
                    <q-item-section class="text-negative">Delete</q-item-section>
                  </q-item>
                </template>
              </q-list>
            </q-menu>
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

const poolScope = ref("all");

const columns = computed(() => [
  { name: "client", label: "Client / Contact", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "priority", label: "Priority", field: "priority", align: "left", sortable: true, default: true, filterable: false },
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "—", align: "left", default: true, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true, filterable: false },
  { name: "industryGroup", label: "Industry Group", field: (r) => r.industryGroup || "—", align: "left", default: true, filterable: false },
  { name: "customerEmail", label: "Client Email", field: (r) => r.customerEmail || "—", align: "left", default: false, filterable: false },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: REMS_STATUS_OPTIONS },
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
