<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Client Forms' }]"
      show-back
      @back="$router.back()"
    />

    <app-data-table
      page-key="rems-client-forms"
      row-key="remsId"
      title="Client Forms"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      default-sort-by="submittedOnUtc"
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

      <template #body-cell-submittedOnUtc="cell">
        <q-td :props="cell">{{ cell.row.submittedOnUtc ? fmt.formatDateTime(cell.row.submittedOnUtc) : "—" }}</q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <!-- Read-only submitted-form review — SEPARATE from editable engagement setup (AC-REMS-013.2/3, 023.7). -->
          <q-btn
            flat round dense color="primary" icon="o_visibility"
            :disable="!cell.row.submitted" @click="openView(cell.row)"
          >
            <q-tooltip>{{ cell.row.submitted ? "View submitted form" : "The client has not submitted this form yet" }}</q-tooltip>
          </q-btn>
          <q-btn flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 210px;">
                <q-item clickable :disable="!cell.row.submitted" @click="cell.row.submitted && openView(cell.row)">
                  <q-item-section avatar><q-icon name="o_visibility" /></q-item-section>
                  <q-item-section>
                    View submitted form
                    <q-tooltip v-if="!cell.row.submitted">Available once the client submits their form</q-tooltip>
                  </q-item-section>
                </q-item>
                <q-separator />
                <!-- Distinct editable engagement action, kept apart from the read-only review. -->
                <q-item
                  clickable :disable="!cell.row.submitted"
                  @click="cell.row.submitted && openEngagement(cell.row)"
                >
                  <q-item-section avatar><q-icon name="o_engineering" /></q-item-section>
                  <q-item-section>
                    Engagement Setup
                    <q-tooltip v-if="!cell.row.submitted">Available once the client submits their form</q-tooltip>
                  </q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_dynamic_form" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No client forms yet</div>
          <div>Once an EMS form is built and sent, it appears here for review.</div>
        </div>
      </template>
    </app-data-table>

    <submitted-form-dialog v-model="viewOpen" :rems-id="viewRemsId" />
  </q-page>
</template>

<script setup>
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useListTable } from "composables/useListTable";
import { useDateFormat } from "composables/useDateFormat";

import AppListHeader from "components/common/AppListHeader.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import SubmittedFormDialog from "modules/rems/components/SubmittedFormDialog.vue";

const router = useRouter();
const notify = useNotify();
const fmt = useDateFormat();

const columns = computed(() => [
  { name: "remsNumber", label: "Request ID", field: "remsNumber", align: "left", sortable: true, default: true },
  { name: "clientName", label: "Client", field: "clientName", align: "left", sortable: true, default: true },
  { name: "submitted", label: "Form", field: "submitted", align: "left", sortable: true, default: true },
  { name: "submittedOnUtc", label: "Submitted", field: "submittedOnUtc", align: "left", sortable: true, default: true },
  { name: "assignedAdmin", label: "Assigned Admin", field: (r) => r.assignedAdmin?.name || "—", align: "left", default: true },
  { name: "cse", label: "CSE", field: (r) => r.cse?.name || "—", align: "left", default: true },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const { rows, loading, totalRecords, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    remsApi.clientForms({ page, limit }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// ---- Read-only submitted-form review ----
const viewOpen = ref(false);
const viewRemsId = ref(null);
const openView = (row) => {
  if (!row.submitted) return;
  viewRemsId.value = row.remsId;
  viewOpen.value = true;
};

// ---- Editable engagement setup (WO-117 workspace; distinct from the read-only review) ----
const openEngagement = (row) => {
  if (!row.submitted) return;
  router.push(`/rems/engagements/${row.remsId}`);
};
</script>
