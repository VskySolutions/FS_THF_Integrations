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
      <!-- The two ways to read this queue, beside the column picker in the table's own top bar. It is one
           list either way — "All" is every request that has reached the admins, mine and everybody
           else's and the ones nobody has taken; "Assigned to me" is the slice that is my work. Server-
           side, like every other filter here, so it narrows the whole set rather than the loaded page. -->
      <template #actions>
        <q-btn-toggle
          v-model="assignment"
          no-caps unelevated dense
          toggle-color="primary" color="grey-3" text-color="grey-8"
          :options="ASSIGNMENT_FILTERS"
        />
      </template>

      <!-- The number opens the request. It used to open the client's form in a modal, which is where that
           form USED to live; it is a pane of the request page now, so the request is the one place the
           number can send you that has both. -->
      <template #body-cell-remsNumber="cell">
        <q-td :props="cell">
          <q-btn
            flat dense no-caps color="primary" class="text-weight-medium"
            :label="cell.row.remsNumber" @click="openRequest(cell.row, 'view')"
          >
            <q-tooltip>Open the request</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #body-cell-clientName="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.clientName || "—" }}</div>
        </q-td>
      </template>

      <!-- Whether the client's answers are in (AC-REMS-013.1 / 023.5). Worded from the firm's side —
           "Received" is what the admin reading this list wants to know, and the client's own act of
           submitting is already named by the Received On date beside it. -->
      <template #body-cell-submitted="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.submitted ? 'positive' : 'grey-6'">
            {{ cell.row.submitted ? "Received" : "Not received" }}
          </q-badge>
        </q-td>
      </template>

      <!-- The status the request is IN, except that one nobody has picked up says so. That is the whole
           point of this list now: the initiator submits to the admins, not to one of them, so the row has
           to distinguish "on somebody's desk" from "on nobody's". -->
      <template #body-cell-requestStatus="cell">
        <q-td :props="cell">
          <q-badge :color="requestStatusColor(statusRow(cell.row))">
            {{ requestStatusLabel(statusRow(cell.row)) }}
          </q-badge>
        </q-td>
      </template>

      <!-- Who holds the request. The same "Waiting for pickup" the status column shows, said in the
           column that is actually about the assignment — the two are one fact, and a row that reads
           "Waiting for pickup — —" would leave the reader wondering which of them was the answer. -->
      <template #body-cell-assignedAdmin="cell">
        <q-td :props="cell">
          <span v-if="cell.row.assignedAdmin?.name">{{ cell.row.assignedAdmin.name }}</span>
          <q-badge v-else color="amber-8">Waiting for pickup</q-badge>
        </q-td>
      </template>

      <template #body-cell-submittedOnUtc="cell">
        <q-td :props="cell">{{ cell.row.submittedOnUtc ? fmt.formatDateTime(cell.row.submittedOnUtc) : "—" }}</q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <!-- Claiming the request, and the first thing to do with one nobody holds — so it leads the row
               rather than sitting behind the read actions. Filled, unlike everything beside it: on an
               unclaimed row it is the only action that changes anything. -->
          <q-btn
            v-if="cell.row.canPickUp"
            unelevated dense no-caps color="amber-8" icon="o_pan_tool_alt" label="Pick up"
            class="q-px-sm q-mr-xs" :loading="pickingUp === cell.row.remsId" @click.stop="pickUp(cell.row)"
          >
            <q-tooltip>Take this request on — its engagement setup becomes yours to work</q-tooltip>
          </q-btn>

          <!-- The undo of Pick up, on the row it was pressed on. Both are asked before they run, but a
               dialog only catches the misclick that is noticed — and until this button existed the way
               back from one that was not was to open the request and find Hand back in its header.
               Outlined, and only on a request this caller actually holds: it is a correction, not a step
               in the work. -->
          <q-btn
            v-if="cell.row.canHandBack"
            outline dense no-caps color="grey-8" icon="o_undo" label="Hand back"
            class="q-px-sm q-mr-xs" :loading="handingBack === cell.row.remsId" @click.stop="handBack(cell.row)"
          >
            <q-tooltip>Put this back in the queue for another admin to pick up</q-tooltip>
          </q-btn>

          <!-- View and Edit are the same page in two modes. Separate actions because they are separate
               intentions: reading a request should never put a form on screen — and they are gated apart
               for the same reason. Every admin may READ any request in this queue, including one nobody
               has picked up; that is how you decide whether to take it. Editing is the holder's.
               Neither waits on the client: the page opens on the intake the initiator filled in, and the
               client's answers land in it when they arrive. -->
          <q-btn
            flat round dense color="primary" icon="o_visibility"
            @click="openRequest(cell.row, 'view')"
          >
            <q-tooltip>View</q-tooltip>
          </q-btn>
          <q-btn
            flat round dense color="primary" icon="o_edit"
            :disable="!!editBlocked(cell.row)" @click="openRequest(cell.row, 'edit')"
          >
            <q-tooltip>{{ editBlocked(cell.row) || "Edit" }}</q-tooltip>
          </q-btn>
          <!-- A "View the client's form" action stood here, opening the submission in a modal. The
               submission is a pane of the request page now, beside the setup it is read against, so View
               and Edit above both land on it. -->

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
          <div class="text-subtitle1 q-mb-xs">
            {{ assignment === "mine" ? "You have not picked anything up" : "Nothing to review yet" }}
          </div>
          <div>
            {{ assignment === "mine"
              ? "Switch to All to see the requests waiting for an admin to pick them up."
              : "A request appears here once its initiator has sent the intake form to their client." }}
          </div>
        </div>
      </template>
    </app-data-table>

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
import { useConfirm } from "composables/useConfirm";
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
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";
import EmailLogDialog from "modules/rems/components/EmailLogDialog.vue";

const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const auditColumns = useAuditColumns();
const { has } = usePermissions();
const { requestStatusLabel, requestStatusColor, statusFilterOptions, engagementOwnerDenial } = useRemsMeta();

const canReadEmailLog = computed(() => has(Permissions.RemsEmailLogRead));

// The status helpers read a REQUEST shape (`status` + `assignedAdmin`); these rows call the same field
// `requestStatus`, because the row is about the client's form and the request's status is context on it.
const statusRow = (row) => ({ status: row?.requestStatus, assignedAdmin: row?.assignedAdmin });

// Why the row's way into the request is shut, or null when it is open. Editing is the holder's alone,
// which is the rule the server enforces on the setup; saying so on the button beats letting the click end
// in a 403. Reading is open to every admin whoever holds the request — this is a shared queue, and
// deciding whether to pick something up means being able to look at it first.
//
// Neither waits on the client any more. Both used to be shut until the form came back, which left an admin
// unable to open — or correct — a request whose intake was already there to read, at exactly the point in
// its life when a mistake in it is still cheap to fix. What genuinely needs a submission is the button
// beside these, which shows the client's own answers, and that one is still gated on it.
const editBlocked = (row) => engagementOwnerDenial(row);

// REMS number and client are covered by the quick search, so they get no duplicate filter box of their
// own; the name/date columns the server cannot narrow on opt out entirely.
const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "clientName", label: "Client", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "submitted", label: "Form", field: "submitted", align: "left", sortable: true, default: true, filterOptions: REMS_FORM_SUBMITTED_OPTIONS },
  // On by default now. It is where a row says "Waiting for pickup", which is the one thing an admin
  // opening this list is looking for.
  { name: "requestStatus", label: "Request Status", field: "requestStatus", align: "left", default: true, filterOptions: statusFilterOptions.value },
  { name: "submittedOnUtc", label: "Received On", field: "submittedOnUtc", align: "left", sortable: true, default: true, filterable: false },
  // The cell renders the pickup badge when nobody holds it, so the field only has to feed sorting/export.
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "Waiting for pickup", align: "left", default: true, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

// The quick filter. NOT part of the column filters below: those are the drawer's, each with a chip and a
// Clear, and this one is neither — it is which list you are looking at, and there is always one of the two
// selected. "All" leads because it is the whole queue, waiting-for-pickup rows included, and seeing those
// is what this list is for; narrowing to your own work is the deliberate second step.
const ASSIGNMENT_FILTERS = [
  { label: "All", value: "all" },
  { label: "Assigned to me", value: "mine" }
];
const assignment = ref("all");

const { rows, loading, totalRecords, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.clientForms({
      page,
      limit,
      search: search.value || undefined,
      // A column filter's value is always a string; the API takes a bool.
      submitted: filters.submitted ? filters.submitted === "true" : undefined,
      requestStatus: filters.requestStatus || undefined,
      assignment: assignment.value
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Server-side, like every other REMS list: the pager counts the whole filtered set, not the loaded page.
const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters, assignment], reload, { deep: true });

// ---- Picking a request up ----
// The whole of the new assignment model from this list's side: nobody was named at intake, so a request
// becomes an admin's by that admin taking it. Tracked per row so only the pressed button spins.
//
// Confirmed, the way Hand back is: one click on a list of near-identical rows is an easy click to make
// on the wrong row, and the request stops being available to the other admins the moment it lands.
const pickingUp = ref(null);
const pickUp = async (row) => {
  const ok = await confirm({
    title: "Pick this request up",
    message: `${row.remsNumber} becomes yours and its engagement setup opens for you to work. No other ` +
      "admin can take it while you hold it — Hand back is what returns it to the queue. Continue?",
    confirmLabel: "Pick up"
  });
  if (!ok) return;
  pickingUp.value = row.remsId;
  try {
    await remsApi.pickUp(row.remsId);
    notify.success(`${row.remsNumber} is yours. Its engagement setup is now open to you.`);
    // Reloaded rather than patched in place: the row's status badge, its assigned admin and its Edit
    // action all turn on the assignment, and another admin may have taken something else meanwhile.
    await load();
  } catch (err) {
    // Most often "somebody else got there first", which the server words for us.
    notify.error(getApiErrorMessage(err));
    await load();
  } finally {
    pickingUp.value = null;
  }
};

// ---- Handing one back ----
// The counterpart of Pick up: a request taken by mistake goes straight back to the queue from the row it
// was taken on. Confirmed like it — the setup goes read-only to whoever gives it up, and another admin
// may take it immediately.
const handingBack = ref(null);
const handBack = async (row) => {
  const ok = await confirm({
    title: "Hand back to the queue",
    message: `${row.remsNumber} goes back to EMS Review as waiting for pickup, and its engagement setup ` +
      "goes read-only to you. Any admin can take it from there — including you. Continue?",
    confirmLabel: "Hand back"
  });
  if (!ok) return;
  handingBack.value = row.remsId;
  try {
    await remsApi.handBack(row.remsId);
    notify.success(`${row.remsNumber} is waiting for pickup again.`);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    handingBack.value = null;
    // Reloaded either way: the row's status badge, its assigned admin and its actions all turn on the
    // assignment, and a failure most often means somebody else has already changed it.
    await load();
  }
};

// ---- The request itself ----
// The review surface IS the form: it opens both the client's answers and the engagement setup, with the
// admin's send-back and route-for-approval actions on it. `mode` picks which of the two routes it lands
// on — a record to read, or a form to change. Open whether or not the client has answered: the page shows
// the intake either way, and the parts that need a submission are disabled on the page itself.
const openRequest = (row, mode) => {
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
