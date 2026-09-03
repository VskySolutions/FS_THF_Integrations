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
      :pinned-row-keys="pinnedRowKeys"
      :row-colours="rowColours"
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

      <!-- The number opens the request, which is the one place that carries both the setup and the
           client's own answers (the latter in a pane beside it). -->
      <template #body-cell-remsNumber="cell">
        <q-td :props="cell">
          <!-- The pin the reader put on this row, beside the number rather than only on the button that
               set it: the button is at the far right and the reason the row is at the top is here. -->
          <entity-pinned-mark :pinned="isPinned(cell.row.remsId)" />
          <q-btn
            flat dense no-caps color="primary" class="text-weight-medium"
            :label="cell.row.remsNumber" :to="viewRoute(cell.row)"
          >
            <q-tooltip>Open the request</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <!-- The particle after the name and heavier than it, even here where the whole name is
           already medium: it is what tells one "John Smith" row from the next. The column still SORTS
           and searches on `clientName`, which is the two joined. -->
      <template #body-cell-clientName="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">
            <app-name-with-suffix :name="cell.row.clientName" :suffix="cell.row.clientNameSuffix" />
          </div>
        </q-td>
      </template>

      <!-- Whether the client's answers are in. The row carries a BOOLEAN, but the two states it stands for
           are the REMS.ClientSubmissionState values — so the badge is rendered from those, and a firm that
           rewords or recolours either one sees it here too. -->
      <template #body-cell-submitted="cell">
        <q-td :props="cell">
          <app-option-badge :option="submittedOption(cell.row.submitted)" />
        </q-td>
      </template>

      <!-- The status the request is IN, except that one nobody has picked up says so. That is the whole
           point of this list now: the initiator submits to the admins, not to one of them, so the row has
           to distinguish "on somebody's desk" from "on nobody's". -->
      <template #body-cell-requestStatus="cell">
        <q-td :props="cell">
          <!-- What the stage means, a hover away: the tooltip is the status option's own Description
               (Administration → Option Sets), and "Waiting for pickup" — which is this application's
               refinement rather than a stored status — explains itself. -->
          <app-option-badge :option="requestStatusOption(statusRow(cell.row))" />
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
        <q-td :props="cell">
          <!-- Claiming the request, and the first thing to do with one nobody holds — so it leads the row
               rather than sitting behind the read actions.
               An icon like everything beside it. What used to set it apart was its weight — a filled
               button among flat ones — and that is now carried by its COLOUR: amber, the same amber the
               row's own "Waiting for pickup" badge is in, so the badge and the action that answers it
               read as one thing. Its NAME leads the tooltip, because an icon on its own does not carry
               one. -->
          <q-btn
            v-if="cell.row.canPickUp" type="a"
            flat round dense color="amber-8" icon="o_pan_tool_alt"
            :loading="pickingUp === cell.row.remsId" @click.stop="pickUp(cell.row)"
          >
            <q-tooltip>Pick up — take this request on, and its engagement setup becomes yours to work</q-tooltip>
          </q-btn>

          <!-- The undo of Pick up, on the row it was pressed on. Both are asked before they run, but a
               dialog only catches the misclick that is noticed — and until this button existed the way
               back from one that was not was to open the request and find Hand back in its header.
               Grey among the coloured ones, and only on a request this caller actually holds: it is a
               correction, not a step in the work. -->
          <q-btn
            v-if="cell.row.canHandBack" type="a"
            flat round dense color="grey-8" icon="o_undo"
            :loading="handingBack === cell.row.remsId" @click.stop="handBack(cell.row)"
          >
            <q-tooltip>Hand back — put this in the queue for another admin to pick up</q-tooltip>
          </q-btn>

          <!-- View and Edit are the same page in two modes. Separate actions because they are separate
               intentions: reading a request should never put a form on screen — and they are gated apart
               for the same reason. Every admin may READ any request in this queue, including one nobody
               has picked up; that is how you decide whether to take it. Editing is the holder's.
               Neither waits on the client: the page opens on the intake the initiator filled in, and the
               client's answers land in it when they arrive. -->
          <q-btn
            flat round dense color="primary" icon="o_visibility" :to="viewRoute(cell.row)"
          >
            <q-tooltip>View</q-tooltip>
          </q-btn>
          <q-btn
            flat round dense color="primary" icon="o_edit" :to="editRoute(cell.row)"
            :disable="!!editBlocked(cell.row)"
          >
            <q-tooltip>{{ editBlocked(cell.row) || "Edit" }}</q-tooltip>
          </q-btn>
          <!-- What the client has been emailed about this request and what came back, plus the reminder
               for one who still has not answered. Every row here has a form, so there is always a log to
               open — an empty one is itself the answer for a form nobody has sent yet. -->
          <q-btn
            v-if="canReadEmailLog" type="a"
            flat round dense color="primary" icon="o_mark_email_read" @click.stop="openEmailLog(cell.row)"
          >
            <q-tooltip>Email log</q-tooltip>
          </q-btn>
          <q-btn type="a" flat round dense color="primary" icon="o_forum" @click.stop="openConversation(cell.row)">
            <q-tooltip>Conversation</q-tooltip>
          </q-btn>
          <!-- The reader's own marks on this row, sitting with the actions rather than apart from them:
               everything before them acts on the REQUEST, and these two are private to whoever is
               looking. On a shared queue that distinction is the whole point —
               "mine to come back to" is not the same as "assigned to me". -->
          <entity-row-marks
            v-if="canMarkRows"
            :pinned="isPinned(cell.row.remsId)"
            :colour="colourOf(cell.row.remsId)"
            :palette="markPalette"
            :limit-reached="pinLimitReached"
            :limit="MAX_PINS_PER_TYPE"
            :busy="markBusyId === cell.row.remsId"
            @toggle-pin="togglePin(cell.row.remsId, cell.row.remsNumber)"
            @set-colour="applyColour(cell.row.remsId, $event, cell.row.remsNumber)"
          />
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
import { remsApi, getApiErrorMessage, EntityType } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useRowPersonalisation, MAX_PINS_PER_TYPE } from "composables/uf/useRowPersonalisation";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";
import { useAuditColumns } from "composables/useAuditColumns";
import { useRemsMeta } from "modules/rems/useRemsMeta";

import AppListHeader from "components/common/AppListHeader.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import AppNameWithSuffix from "components/common/AppNameWithSuffix.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import EntityPinnedMark from "components/universal/EntityPinnedMark.vue";
import EntityRowMarks from "components/universal/EntityRowMarks.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";
import EmailLogDialog from "modules/rems/components/EmailLogDialog.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const auditColumns = useAuditColumns();
const { has } = usePermissions();
const {
  requestStatusOption, submissionStateOption, statusFilterOptions, engagementOwnerDenial
} = useRemsMeta();

// The "EMS State" column is a boolean on the row, but the two states it stands for are values on
// REMS.ClientSubmissionState — so both the badge and the filter read their words from there rather than
// carrying a pair of hardcoded strings. The filter VALUES stay "true" / "false": that is the server's
// contract for this column, and a column filter's value is always a string.
//
// It is a NARROWER question than the column of the same name on My Requests, which reads REMS.FormStatus
// and can say Not started or Sent. Nothing reaches this queue until its form has gone out, so the only
// two answers left here are the two this list draws: still with the client, or back in hand.
const submittedOption = (submitted) => submissionStateOption(submitted ? "Submitted" : "AwaitingCustomer");
const submittedFilterOptions = computed(() => [
  { label: submittedOption(true).label, value: "true" },
  { label: submittedOption(false).label, value: "false" }
]);

const canReadEmailLog = computed(() => has(Permissions.RemsEmailLogRead));

// The status helpers read a REQUEST shape (`status` + `assignedAdmin`); these rows call the same field
// `requestStatus`, because the row is about the client's form and the request's status is context on it.
const statusRow = (row) => ({ status: row?.requestStatus, assignedAdmin: row?.assignedAdmin });

// Why the row's way into the request is shut, or null when it is open. Editing is the holder's alone,
// which is the rule the server enforces on the setup; saying so on the button beats letting the click end
// in a 403. Reading is open to every admin whoever holds the request — this is a shared queue, and
// deciding whether to pick something up means being able to look at it first.
//
// Neither waits on the client. Gating them on the submission would leave an admin unable to open — or
// correct — a request whose intake was already there to read, at exactly the point where a mistake in it
// is still cheap to fix. What genuinely needs a submission is the button beside these.
const editBlocked = (row) => engagementOwnerDenial(row);

// REMS number and client are covered by the quick search, so they get no duplicate filter box of their
// own; the name/date columns the server cannot narrow on opt out entirely.
const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true, filterable: false },
  { name: "clientName", label: "Client", field: "clientName", align: "left", sortable: true, default: true, filterable: false },
  { name: "submitted", label: "EMS State", field: "submitted", align: "left", sortable: true, default: true, filterOptions: submittedFilterOptions.value },
  // On by default now. It is where a row says "Waiting for pickup", which is the one thing an admin
  // opening this list is looking for.
  { name: "requestStatus", label: "Request Status", field: "requestStatus", align: "left", default: true, filterOptions: statusFilterOptions.value },
  { name: "submittedOnUtc", label: "Received On", field: "submittedOnUtc", align: "left", sortable: true, default: true, filterable: false },
  // The cell renders the pickup badge when nobody holds it, so the field only has to feed sorting/export.
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "Waiting for pickup", align: "left", default: true, filterable: false },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "left" }
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
  pageKey: "rems-ems-review",
  fetcher: ({ page, limit, sortBy, descending }) =>
    remsApi.clientForms({
      page,
      limit,
      sortBy,
      descending,
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

// ---- The reader's own marks on these rows ----
// A pin floats a row to the top of the page and a colour tints it, both stored against the USER — so on
// a queue every admin shares, neither says anything to anybody else. Every caller here already holds
// rems.engagements.manage; the check is on rems.requests.read, which is what the UF endpoints actually
// gate on (UniversalFeatureEntityAccess), so the buttons appear exactly where they will work.
const canMarkRows = computed(() => has(Permissions.RemsRequestsRead));

const {
  palette: markPalette, pinnedRowKeys, pinLimitReached, isPinned, togglePin,
  colours: rowColours, colourOf, applyColour, busyId: markBusyId, sync: syncMarks
} = useRowPersonalisation(EntityType.Rems);

// After every load, never per row: the colours come back for the whole page in one read, and the pins
// are the user's own small set, fetched once.
watch(rows, (list) => {
  if (canMarkRows.value) syncMarks(list.map((r) => r.remsId));
});

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
// LINKS, not click handlers. View and Edit go to a known route, so they are written as routes and Quasar
// renders each button as a real <a href> — which is what makes middle-click and "open in new tab" work,
// and what lets an admin working a queue open three requests side by side instead of one at a time.
// A router.push behind @click renders a <button> and none of that is possible.
//
// Everything else on the row stays a button, and correctly: Pick up, Hand back, the email log and the
// conversation are actions and dialogs, not places. A disabled Edit also stays a button — Quasar drops
// the anchor when a link is disabled, which is right, since a link that goes nowhere should not be one.
const viewRoute = (row) => ({ name: "rems_request", params: { id: row.remsId } });
const editRoute = (row) => ({ name: "rems_request_edit", params: { id: row.remsId } });

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
