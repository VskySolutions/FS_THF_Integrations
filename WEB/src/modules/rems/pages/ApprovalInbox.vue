<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'REMS Approvals' }]"
      :search="search"
      show-search
      search-placeholder="Search REMS number or client"
      show-back
      @update:search="search = $event"
      @back="$router.back()"
    />

    <div class="text-body2 text-grey-8 q-mb-md">
      Your approval tasks — the engagements routed to you to review. You only ever see your own tasks; a
      decision is final once made.
    </div>

    <app-data-table
      page-key="rems-approvals"
      row-key="taskId"
      title="My Approval Tasks"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :total-records="filteredRows.length"
      :pagination="pagination"
      default-sort-by="sentOnUtc"
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

      <template #body-cell-sentOnUtc="cell">
        <q-td :props="cell">{{ fmt.formatDateTime(cell.row.sentOnUtc) }}</q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" @click.stop="openTask(cell.row)">
            <q-tooltip>Open task</q-tooltip>
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
  </q-page>
</template>

<script setup>
// The task-isolated REMS Approval Inbox (WO-117 Part B, AC-REMS-019): the caller's OWN approval tasks
// (pending + historical). The backend returns only the caller's tasks, so this surface never exposes another
// approver's work, an approver picker, or impersonation. Clicking a row opens the role-scoped task detail.
import { computed } from "vue";
import { useRouter } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useListTable } from "composables/useListTable";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppDataTable from "components/common/AppDataTable.vue";

const router = useRouter();
const notify = useNotify();
const fmt = useDateFormat();
const { approverRoleLabel, approverRoleIcon, approvalStatusLabel, approvalStatusColor } = useRemsMeta();

const columns = [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true },
  { name: "client", label: "Client / Entity", field: "clientName", align: "left", sortable: true, default: true },
  { name: "role", label: "Your Role", field: "role", align: "left", sortable: true, default: true },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true },
  { name: "sentOnUtc", label: "Sent", field: "sentOnUtc", align: "left", sortable: true, default: true },
  { name: "actions", label: "", field: "actions", align: "right" }
];

// Own-tasks list loads in full (no server pagination); tenant-switch reload comes from useListTable.
const { rows, loading, search, pagination, load, onRequest } = useListTable({
  fetcher: () => remsApi.myApprovalTasks().then((r) => ({ data: r || [], total: (r || []).length })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Client-side quick search across REMS number, client and entity.
const filteredRows = computed(() => {
  const q = (search.value || "").trim().toLowerCase();
  if (!q) return rows.value;
  return rows.value.filter((r) =>
    [r.remsNumber, r.clientName, r.entityName].some((v) => (v || "").toLowerCase().includes(q)));
});

const openTask = (row) => router.push({ name: "rems_approval_task", params: { taskId: row.taskId } });
</script>
