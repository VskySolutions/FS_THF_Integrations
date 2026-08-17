<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'EMS Review' }]"
      :search="search"
      show-search
      search-placeholder="Search REMS number or client"
      show-filters
      :filter-count="filterChips.length"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @back="$router.back()"
    />

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
    </app-filter-drawer>

    <app-data-table
      page-key="rems-ems-review"
      row-key="remsId"
      title="EMS Review"
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
          <q-btn
            v-if="cell.row.submitted"
            flat dense no-caps color="primary" class="text-weight-medium"
            :label="cell.row.remsNumber" @click="openView(cell.row)"
          >
            <q-tooltip>View submitted form</q-tooltip>
          </q-btn>
          <span v-else class="text-weight-medium">{{ cell.row.remsNumber }}</span>
        </q-td>
      </template>

      <template #body-cell-clientName="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.clientName || "—" }}</div>
        </q-td>
      </template>

      <!-- Submitted vs not-submitted indicator (AC-REMS-013.1 / 023.5). -->
      <template #body-cell-submitted="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.submitted ? 'positive' : 'grey-6'">
            {{ cell.row.submitted ? "Submitted" : "Not submitted" }}
          </q-badge>
        </q-td>
      </template>

      <template #body-cell-requestStatus="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.row.requestStatus)">{{ statusLabel(cell.row.requestStatus) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-submittedOnUtc="cell">
        <q-td :props="cell">{{ cell.row.submittedOnUtc ? fmt.formatDateTime(cell.row.submittedOnUtc) : "—" }}</q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <!-- View and Edit are the same page in two modes. Separate actions because they are separate
               intentions: reading a request should never put a form on screen. -->
          <q-btn
            flat round dense color="primary" icon="o_visibility"
            :disable="!!engagementBlocked(cell.row)" @click="openRequest(cell.row, 'view')"
          >
            <q-tooltip>{{ engagementBlocked(cell.row) || "View" }}</q-tooltip>
          </q-btn>
          <q-btn
            flat round dense color="primary" icon="o_edit"
            :disable="!!engagementBlocked(cell.row)" @click="openRequest(cell.row, 'edit')"
          >
            <q-tooltip>{{ engagementBlocked(cell.row) || "Edit" }}</q-tooltip>
          </q-btn>
          <!-- The client's own submitted answers, verbatim and immutable — distinct from the working copy
               the form above edits. -->
          <q-btn
            flat round dense color="primary" icon="o_description"
            :disable="!cell.row.submitted" @click="openView(cell.row)"
          >
            <q-tooltip>{{ cell.row.submitted ? "View submitted form" : "The client has not submitted this form yet" }}</q-tooltip>
          </q-btn>
          <!-- What the client has been emailed about this request and what came back, plus the reminder
               for one who still has not answered. Every row here has a form, so there is always a log to
               open — an empty one is itself the answer for a form nobody has sent yet. -->
          <q-btn
            v-if="canReadEmailLog"
            flat round dense color="primary" icon="o_mark_email_read" @click.stop="openEmailLog(cell.row)"
          >
            <q-tooltip>Email log</q-tooltip>
          </q-btn>
          <q-btn flat round dense color="primary" icon="o_forum" @click.stop="openConversation(cell.row)">
            <q-tooltip>Conversation</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_dynamic_form" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">Nothing to review yet</div>
          <div>A request appears here once its client has submitted their intake form.</div>
        </div>
      </template>
    </app-data-table>

    <submitted-form-dialog v-model="viewOpen" :rems-id="viewRemsId" />
    <conversation-dialog v-model="conversationOpen" :request-id="conversationId" :subtitle="conversationSubtitle" />
    <email-log-dialog v-model="emailLogOpen" :rems-id="emailLogId" :subtitle="emailLogSubtitle" @sent="load" />
  </q-page>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { debounce } from "quasar";
import { useRouter } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";
import { useAuditColumns } from "composables/useAuditColumns";
import { useRemsMeta, REMS_FORM_SUBMITTED_OPTIONS } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import SubmittedFormDialog from "modules/rems/components/SubmittedFormDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";
import EmailLogDialog from "modules/rems/components/EmailLogDialog.vue";

const router = useRouter();
const notify = useNotify();
const fmt = useDateFormat();
const auditColumns = useAuditColumns();
const { has } = usePermissions();
const { statusLabel, statusColor, statusFilterOptions, engagementOwnerDenial } = useRemsMeta();

const canReadEmailLog = computed(() => has(Permissions.RemsEmailLogRead));

// Why Engagement Setup is unavailable on a row, or null when it is open: the client has to have
// submitted, and setup then belongs to whoever picked the request up.
const engagementBlocked = (row) =>
  (row?.submitted ? engagementOwnerDenial(row) : "The client has not submitted this form yet");

// REMS number and client are covered by the quick search, so they get no duplicate filter box of their
// own; the name/date columns the server cannot narrow on opt out entirely.
const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "clientName", label: "Client", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  // Populated only on a subsidiary, so it is off by default like the other context columns.
  { name: "parentClientName", label: "Parent Client", field: (r) => r.parentClientName || "—", align: "left", default: false, filterable: false },
  { name: "submitted", label: "Form", field: "submitted", align: "left", sortable: true, default: true, filterOptions: REMS_FORM_SUBMITTED_OPTIONS },
  // Off by default so the table keeps its current shape; it is in the filter drawer either way.
  { name: "requestStatus", label: "Request Status", field: "requestStatus", align: "left", default: false, filterOptions: statusFilterOptions.value },
  { name: "submittedOnUtc", label: "Submitted", field: "submittedOnUtc", align: "left", sortable: true, default: true, filterable: false },
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "—", align: "left", default: true, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.clientForms({
      page,
      limit,
      search: search.value || undefined,
      // A column filter's value is always a string; the API takes a bool.
      submitted: filters.submitted ? filters.submitted === "true" : undefined,
      requestStatus: filters.requestStatus || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Server-side, like every other REMS list: the pager counts the whole filtered set, not the loaded page.
const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters], reload, { deep: true });

// ---- Read-only submitted-form review ----
const viewOpen = ref(false);
const viewRemsId = ref(null);
const openView = (row) => {
  if (!row.submitted) return;
  viewRemsId.value = row.remsId;
  viewOpen.value = true;
};

// ---- The request itself ----
// The review surface IS the form: it opens both the client's answers and the engagement setup, with the
// admin's send-back and route-for-approval actions on it. `mode` picks which of the two routes it lands
// on — a record to read, or a form to change.
const openRequest = (row, mode) => {
  if (!row.submitted) return;
  router.push({
    name: mode === "edit" ? "rems_request_edit" : "rems_request",
    params: { id: row.remsId }
  });
};

// ---- Conversation ----
// The REQUEST's thread, the same one every other REMS surface opens — so a message left here reaches the
// partner, the pool and the approvers rather than being visible only from this list.
const conversationOpen = ref(false);
const conversationId = ref(null);
const conversationSubtitle = ref("");
const openConversation = (row) => {
  conversationId.value = row.remsId;
  conversationSubtitle.value = rowLabel(row);
  conversationOpen.value = true;
};

// ---- Email log ----
// Every intake-form email sent for this request and what the provider reported back, with Send Reminder
// on it while the client still owes an answer.
const emailLogOpen = ref(false);
const emailLogId = ref(null);
const emailLogSubtitle = ref("");
const openEmailLog = (row) => {
  emailLogId.value = row.remsId;
  emailLogSubtitle.value = rowLabel(row);
  emailLogOpen.value = true;
};

const rowLabel = (row) => [row.remsNumber, row.clientName].filter(Boolean).join(" — ");
</script>
