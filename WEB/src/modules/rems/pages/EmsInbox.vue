<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'EMS Inbox' }]"
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
      page-key="rems-ems-inbox"
      row-key="remsId"
      title="EMS Inbox"
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
          <q-btn flat dense no-caps color="primary" class="text-weight-medium" :label="cell.row.remsNumber" @click="primaryOpen(cell.row)">
            <q-tooltip>{{ openHint(cell.row) }}</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #body-cell-clientName="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.clientName || "—" }}</div>
        </q-td>
      </template>

      <template #body-cell-engagementType="cell">
        <q-td :props="cell">{{ typeLabel(cell.row.engagementType) }}</q-td>
      </template>

      <template #body-cell-requestStatus="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.row.requestStatus)">{{ statusLabel(cell.row.requestStatus) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-formStatus="cell">
        <q-td :props="cell">
          <q-badge :color="emsStateColor(cell.row.formStatus)">{{ emsStateLabel(cell.row.formStatus) }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-formCreatedBy="cell">
        <q-td :props="cell">{{ cell.row.formCreatedBy?.name || "—" }}</q-td>
      </template>

      <template #body-cell-formSentOnUtc="cell">
        <q-td :props="cell">{{ cell.row.formSentOnUtc ? fmt.formatDateTime(cell.row.formSentOnUtc) : "—" }}</q-td>
      </template>

      <!-- Sent / delivery / open info, only when the provider has reported it (AC-REMS-023.4). -->
      <template #body-cell-latestEvent="cell">
        <q-td :props="cell">
          <template v-if="cell.row.latestEmailEventType">
            <q-badge :color="emailEventColor(cell.row.latestEmailEventType)">
              {{ emailEventLabel(cell.row.latestEmailEventType) }}
            </q-badge>
            <div class="text-caption text-grey-7">{{ fmt.formatDateTime(cell.row.latestEmailEventOnUtc) }}</div>
          </template>
          <template v-else>—</template>
        </q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" :to="detailRoute(cell.row)">
            <q-tooltip>View</q-tooltip>
          </q-btn>
          <q-btn
            v-if="canBuildRow(cell.row)" flat round dense color="primary" icon="o_edit_note"
            @click="openBuild(cell.row)"
          >
            <q-tooltip>Build EMS</q-tooltip>
          </q-btn>
          <q-btn
            v-if="canSendRow(cell.row)" flat round dense color="teal-7" icon="o_send" @click="openSend(cell.row)"
          >
            <q-tooltip>Preview &amp; Send</q-tooltip>
          </q-btn>
          <q-btn
            v-if="showEngagement" flat round dense color="primary" icon="o_work"
            :disable="cell.row.formStatus !== 'Submitted'" @click="openEngagement(cell.row)"
          >
            <q-tooltip>
              {{ cell.row.formStatus === "Submitted" ? "Engagement Setup" : "Available once the client submits their form" }}
            </q-tooltip>
          </q-btn>
          <q-btn
            v-if="showEmailLog(cell.row)" flat round dense color="primary" icon="o_history"
            @click="openEmailLog(cell.row)"
          >
            <q-tooltip>Email Log</q-tooltip>
          </q-btn>
          <q-btn flat round dense color="primary" icon="o_forum" @click.stop="openConversation(cell.row)">
            <q-tooltip>Notes</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_move_to_inbox" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No EMS forms yet</div>
          <div>Build an EMS form from a request in the Admin Pool and it will appear here.</div>
        </div>
      </template>
    </app-data-table>

    <send-ems-dialog
      v-model="sendOpen" :rems-id="actionRemsId" :subtitle="actionSubtitle"
      :can-view-email-log="canViewEmailLog" @sent="onSent" @view-log="viewLogFromSend"
    />
    <email-log-dialog v-model="logOpen" :rems-id="actionRemsId" :subtitle="actionSubtitle" />
    <conversation-dialog v-model="conversationOpen" :request-id="conversationId" :subtitle="conversationSubtitle" />
  </q-page>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { debounce } from "quasar";
import { useRouter } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";
import { useAuditColumns } from "composables/useAuditColumns";
import { useRemsMeta, REMS_FORM_STATE_OPTIONS } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import SendEmsDialog from "modules/rems/components/SendEmsDialog.vue";
import EmailLogDialog from "modules/rems/components/EmailLogDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";

const router = useRouter();
const notify = useNotify();
const fmt = useDateFormat();
const auditColumns = useAuditColumns();
const { has } = usePermissions();
const {
  typeLabel, statusLabel, statusColor, emsStateLabel, emsStateColor, emailEventLabel, emailEventColor,
  statusFilterOptions
} = useRemsMeta();

const canViewEmailLog = computed(() => has(Permissions.RemsEmailLogRead));
const showEngagement = computed(() => has(Permissions.RemsEngagementsManage));

const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "clientName", label: "Client", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "engagementType", label: "Engagement Type", field: "engagementType", align: "left", default: true, filterable: false },
  { name: "requestStatus", label: "Request Status", field: "requestStatus", align: "left", default: true, filterOptions: statusFilterOptions.value },
  { name: "formStatus", label: "EMS Form State", field: "formStatus", align: "left", default: true, filterOptions: REMS_FORM_STATE_OPTIONS },
  { name: "formCreatedBy", label: "Form Creator", field: (r) => r.formCreatedBy?.name || "—", align: "left", default: true, filterable: false },
  { name: "formSentOnUtc", label: "Sent", field: "formSentOnUtc", align: "left", sortable: true, default: true, filterable: false },
  { name: "latestEvent", label: "Delivery / Open", field: "latestEmailEventType", align: "left", default: true, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.inbox({
      page,
      limit,
      search: search.value || undefined,
      formState: filters.formStatus || undefined,
      requestStatus: filters.requestStatus || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Search and filters are applied server-side, so the pager counts the whole filtered set rather than
// whichever page happens to be loaded.
const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters], reload, { deep: true });

// ---- State-based row navigation (AC-REMS-009.5) ----
const detailRoute = (row) => ({ name: "rems_request_detail", params: { id: row.remsId } });
const openBuild = (row) => router.push({ name: "rems_build_ems", params: { id: row.remsId } });
const openEngagement = (row) => router.push(`/rems/engagements/${row.remsId}`);

const canSendRow = (row) => has(Permissions.RemsFormsSend) && row.formStatus === "Saved";
const showEmailLog = (row) =>
  has(Permissions.RemsEmailLogRead) &&
  (!!row.formSentOnUtc || !!row.latestEmailEventType || ["Sent", "Submitted"].includes(row.formStatus));
// Once the customer has submitted, the form is finished and the work moves to the engagement workspace,
// so it can no longer be built or rebuilt (mirrors the request detail screen). Keyed on the form's own
// state rather than the request status, which carries on past `customer_submitted` into the approval
// stages — where the form is, if anything, even more finished.
const canBuildRow = (row) => row.formStatus !== "Submitted";

// Where the Request ID itself takes you — the row's most relevant destination, resolved once so the
// tooltip and the click can never disagree: Submitted → Engagement Workspace; Sent/Submitted → Email Log;
// otherwise Build EMS — each skipped when the user lacks that destination's permission. Every one of these
// is also its own button in the actions column; this is the shortcut, not the only way in.
const primaryAction = (row) => {
  if (row.formStatus === "Submitted" && showEngagement.value) {
    return { hint: "Open Engagement Setup", run: () => openEngagement(row) };
  }
  if (["Sent", "Submitted"].includes(row.formStatus) && has(Permissions.RemsEmailLogRead)) {
    return { hint: "View Email Log", run: () => openEmailLog(row) };
  }
  if (canBuildRow(row)) {
    return { hint: "Open Build EMS", run: () => openBuild(row) };
  }
  return null;
};

const primaryOpen = (row) => {
  const action = primaryAction(row);
  if (action) action.run();
  else router.push(detailRoute(row));
};
const openHint = (row) => primaryAction(row)?.hint || "View request";

// ---- Row actions (Send / Email Log dialogs) ----
const actionRemsId = ref(null);
const actionSubtitle = ref("");
const sendOpen = ref(false);
const logOpen = ref(false);

const setAction = (row) => {
  actionRemsId.value = row.remsId;
  actionSubtitle.value = `${row.remsNumber} — ${row.clientName || ""}`.trim();
};
const openSend = (row) => { setAction(row); sendOpen.value = true; };
const openEmailLog = (row) => { setAction(row); logOpen.value = true; };
const viewLogFromSend = () => { logOpen.value = true; };
const onSent = () => load();

// ---- Notes ----
// The REQUEST's thread, the same one every other REMS surface opens — so a note left here reaches the
// partner, the pool and the approvers rather than being visible only from this list.
const conversationOpen = ref(false);
const conversationId = ref(null);
const conversationSubtitle = ref("");
const openConversation = (row) => {
  conversationId.value = row.remsId;
  conversationSubtitle.value = `${row.remsNumber} — ${row.clientName || ""}`.trim();
  conversationOpen.value = true;
};
</script>
