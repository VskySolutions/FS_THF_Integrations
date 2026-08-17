<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'REMS Approvals' }]"
      :search="search"
      show-search
      search-placeholder="Search REMS number, client or entity"
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

    <div class="text-body2 text-grey-8 q-mb-md">
      Your approval tasks — the engagements routed to you to review. You only ever see your own tasks; a
      decision is final once made.
    </div>

    <app-data-table
      page-key="rems-approvals"
      row-key="taskId"
      title="My Approval Tasks"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      default-sort-by="updatedOnUtc"
      @request="onRequest"
      @refresh="load"
      @row-click="(_, row) => openTask(row)"
    >
      <template #body-cell-remsNumber="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.remsNumber || "—" }}</div>
          <div class="text-caption text-grey-7">Round {{ cell.row.roundNumber }}</div>
        </q-td>
      </template>

      <template #body-cell-client="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.clientName || "—" }}</div>
          <div class="text-caption text-grey-7">{{ cell.row.entityName || "—" }}</div>
        </q-td>
      </template>

      <template #body-cell-role="cell">
        <q-td :props="cell">
          <div class="row items-center no-wrap">
            <q-icon :name="approverRoleIcon(cell.row.role)" color="primary" size="18px" class="q-mr-xs" />
            {{ approverRoleLabel(cell.row.role) }}
          </div>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="approvalStatusColor(cell.row.status)">{{ approvalStatusLabel(cell.row.status) }}</q-badge>
        </q-td>
      </template>

      <!-- How far the whole ROUND has got, not just this task: "1/4" reads as one of four approvers done. -->
      <template #body-cell-approvals="cell">
        <q-td :props="cell">
          <q-badge :color="progressColor(cell.row)">
            {{ cell.row.approvedCount }}/{{ cell.row.approverCount }}
          </q-badge>
          <q-tooltip>{{ progressHint(cell.row) }}</q-tooltip>
        </q-td>
      </template>

      <template #body-cell-sentOnUtc="cell">
        <q-td :props="cell">{{ fmt.formatDateTime(cell.row.sentOnUtc) }}</q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" @click.stop="openTask(cell.row)">
            <q-tooltip>Open task</q-tooltip>
          </q-btn>
          <q-btn flat round dense color="primary" icon="o_forum" @click.stop="openConversation(cell.row)">
            <q-tooltip>Conversation</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_task_alt" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No approval tasks</div>
          <div>You have no engagements awaiting your review right now.</div>
        </div>
      </template>
    </app-data-table>

    <conversation-dialog v-model="conversationOpen" :request-id="conversationId" :subtitle="conversationSubtitle" />
  </q-page>
</template>

<script setup>
// The task-isolated REMS Approval Inbox (WO-117 Part B, AC-REMS-019): the caller's OWN approval tasks
// (pending + historical). The backend returns only the caller's tasks, so this surface never exposes another
// approver's work, an approver picker, or impersonation. Clicking a row opens the role-scoped task detail.
import { ref, watch } from "vue";
import { debounce } from "quasar";
import { useRouter } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";
import { useAuditColumns } from "composables/useAuditColumns";
import {
  useRemsMeta, REMS_APPROVER_ROLE_OPTIONS, REMS_APPROVAL_STATUS_OPTIONS
} from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";

const router = useRouter();
const notify = useNotify();
const fmt = useDateFormat();
const auditColumns = useAuditColumns();
const { approverRoleLabel, approverRoleIcon, approvalStatusLabel, approvalStatusColor } = useRemsMeta();

// The identity/date columns are covered by the quick search or cannot be narrowed server-side, so they
// opt out of the filter drawer; role and decision state are the two worth filtering on.
const columns = [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "client", label: "Client / Entity", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "role", label: "Your Role", field: "role", align: "left", sortable: true, default: true, filterOptions: REMS_APPROVER_ROLE_OPTIONS },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true, filterOptions: REMS_APPROVAL_STATUS_OPTIONS },
  // Sorts on how much of the round is still outstanding, so the ones closest to done rise together.
  {
    name: "approvals",
    label: "Approvals",
    field: (r) => (r.approverCount || 0) - (r.approvedCount || 0),
    align: "left",
    sortable: true,
    default: true,
    filterable: false
  },
  { name: "sentOnUtc", label: "Sent", field: "sentOnUtc", align: "left", sortable: true, default: true, filterable: false },
  // Off by default — the composite cells above already carry the entity and round number — but offered
  // in the Columns menu so nothing the row returns is unreachable.
  { name: "entityName", label: "Entity", field: "entityName", align: "left", sortable: true, default: false, filterable: false },
  { name: "roundNumber", label: "Round", field: (r) => `#${r.roundNumber}`, align: "left", sortable: true, default: false, filterable: false },
  { name: "roundStatus", label: "Round Status", field: (r) => approvalStatusLabel(r.roundStatus), align: "left", default: false, filterable: false },
  { name: "decidedOnUtc", label: "Decided", field: (r) => (r.decidedOnUtc ? fmt.formatDateTime(r.decidedOnUtc) : "—"), align: "left", sortable: true, default: false, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

// Paged and filtered SERVER-side, like every other REMS list. It used to load every task the caller had
// ever been routed and search them in the browser, which quietly stopped scaling as an approver's history
// grew — and made the pager count the loaded page rather than the matching set.
const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.myApprovalTasks({
      page,
      limit,
      search: search.value || undefined,
      role: filters.role || undefined,
      status: filters.status || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters], reload, { deep: true });

// Round progress. A rejection ENDS the round, so the undecided tasks never decide — "awaiting" is only
// meaningful while the round is still open, and the counts must not imply otherwise.
const pendingOf = (row) => Math.max(0, (row.approverCount || 0) - (row.approvedCount || 0) - (row.rejectedCount || 0));

const progressColor = (row) => {
  if (row.rejectedCount > 0) return "negative";
  if (row.approverCount > 0 && row.approvedCount === row.approverCount) return "positive";
  return "primary";
};

const progressHint = (row) => {
  const parts = [`${row.approvedCount || 0} of ${row.approverCount || 0} approved`];
  if (row.rejectedCount > 0) parts.push(`${row.rejectedCount} rejected — the round ended`);
  else if (pendingOf(row) > 0) parts.push(`${pendingOf(row)} still to decide`);
  return parts.join(" · ");
};

const openTask = (row) => router.push({ name: "rems_approval_task", params: { taskId: row.taskId } });

// ---- Conversation ----
// The REQUEST's thread, the same one the task detail shows under the checklist — so an approver can raise
// a question from the inbox and have the partner and CSE see it, without opening the task first.
const conversationOpen = ref(false);
const conversationId = ref(null);
const conversationSubtitle = ref("");
const openConversation = (row) => {
  conversationId.value = row.remsId;
  conversationSubtitle.value = `${row.remsNumber} — ${row.clientName || ""}`.trim();
  conversationOpen.value = true;
};
</script>
