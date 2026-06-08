<template>
  <q-page padding>
    <app-breadcrumbs :items="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Integration Jobs' }]" />

    <div class="row items-center q-mb-md q-gutter-sm">
      <div class="text-h5 text-weight-bold">Integration Jobs</div>
      <q-space />
      <q-btn unelevated no-caps color="primary" icon="o_play_arrow" label="Trigger Import" @click="openTrigger" />
    </div>

    <q-tabs v-model="tab" align="left" class="text-primary q-mb-sm" dense narrow-indicator>
      <q-tab name="jobs" label="Jobs" no-caps />
      <q-tab name="retries" label="Retry Queue" no-caps />
    </q-tabs>
    <q-separator class="q-mb-md" />

    <q-tab-panels v-model="tab" animated>
      <!-- Jobs -->
      <q-tab-panel name="jobs" class="q-pa-none">
        <div class="row items-center q-mb-sm">
          <app-filter-drawer :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
            <app-select v-model="filters.status" :options="statusOptions" label="Status" class="q-mb-md" />
            <q-input v-model="filters.interfaceName" outlined dense clearable label="Interface name" class="q-mb-md" />
            <app-date-picker v-model="filters.fromDate" label="From date" :max-date="filters.toDate" class="q-mb-md" />
            <app-date-picker v-model="filters.toDate" label="To date" :min-date="filters.fromDate" />
          </app-filter-drawer>
        </div>

        <app-data-table
          page-key="jobs"
          row-key="jobId"
          title="Jobs"
          :rows="jobs"
          :columns="jobColumns"
          :loading="loadingJobs"
          :total-records="jobsTotal"
          :pagination="jobsPagination"
          @request="onJobsRequest"
          @refresh="loadJobs"
        >
          <template #body-cell-status="cell">
            <q-td :props="cell"><q-badge :color="statusColor(cell.value)">{{ cell.value }}</q-badge></q-td>
          </template>
          <template #body-cell-actions="cell">
            <q-td :props="cell" class="text-right">
              <q-btn flat round dense icon="o_more_vert">
                <q-menu auto-close>
                  <q-list style="min-width: 160px;">
                    <q-item clickable @click="openDetail(cell.row)">
                      <q-item-section avatar><q-icon name="o_visibility" /></q-item-section>
                      <q-item-section>View detail</q-item-section>
                    </q-item>
                    <q-item clickable :disable="!isRetryable(cell.row.status)" @click="retry(cell.row.jobId)">
                      <q-item-section avatar><q-icon name="o_replay" /></q-item-section>
                      <q-item-section>Retry</q-item-section>
                      <q-tooltip v-if="!isRetryable(cell.row.status)">Job cannot be retried while it is in this state</q-tooltip>
                    </q-item>
                  </q-list>
                </q-menu>
              </q-btn>
            </q-td>
          </template>
        </app-data-table>
      </q-tab-panel>

      <!-- Retry queue -->
      <q-tab-panel name="retries" class="q-pa-none">
        <app-data-table
          page-key="retries"
          row-key="jobId"
          title="Retry queue"
          :rows="retries"
          :columns="retryColumns"
          :loading="loadingRetries"
          :total-records="retriesTotal"
          :pagination="retriesPagination"
          @request="onRetriesRequest"
          @refresh="loadRetries"
        >
          <template #no-data>
            <div class="full-width column flex-center q-pa-lg text-grey-6">
              <q-icon name="o_inbox" size="32px" class="q-mb-sm" />
              No jobs pending retry
            </div>
          </template>
          <template #body-cell-actions="cell">
            <q-td :props="cell" class="text-right">
              <q-btn flat dense no-caps color="primary" icon="o_replay" label="Retry" @click="retry(cell.row.jobId)" />
            </q-td>
          </template>
        </app-data-table>
      </q-tab-panel>
    </q-tab-panels>

    <!-- Job detail -->
    <app-view-drawer v-model="detailOpen" title="Job detail" :fields="detailFields">
      <q-separator class="q-my-sm" />
      <div class="text-subtitle2 text-grey-7 q-mb-xs">Log entries</div>
      <div v-if="detailLogs.length">
        <q-expansion-item
          v-for="log in detailLogs"
          :key="log.id"
          :label="log.message"
          :caption="log.level"
          dense
        >
          <q-card>
            <q-card-section class="text-caption">{{ formatDate(log.createdDate) }}</q-card-section>
          </q-card>
        </q-expansion-item>
      </div>
      <div v-else class="text-grey-6 text-body2">No log entries.</div>
    </app-view-drawer>

    <!-- Trigger import -->
    <app-form-drawer v-model="triggerOpen" title="Trigger Import" :saving="triggering" save-label="Queue import" @submit="submitTrigger" @cancel="triggerOpen = false">
      <q-banner v-if="credBanner" dense class="bg-red-1 text-negative q-mb-md">
        <template #avatar><q-icon name="o_error" color="negative" /></template>
        Credentials are not configured. Configure them under Tenants → Credentials before importing.
      </q-banner>
      <app-select v-model="triggerFlow" :options="flowOptions" label="Flow type *" :clearable="false" />
    </app-form-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onBeforeUnmount } from "vue";
import { date } from "quasar";
import { jobApi, logApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppViewDrawer from "components/common/AppViewDrawer.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppDatePicker from "components/common/AppDatePicker.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";

const notify = useNotify();
const { confirm } = useConfirm();

const tab = ref("jobs");

const statusOptions = [
  "Created", "Running", "Completed", "PartiallyFailed", "Failed", "PermanentlyFailed"
].map((s) => ({ label: s, value: s }));

const statusColor = (status) => ({
  Completed: "positive",
  Failed: "negative",
  PermanentlyFailed: "deep-orange-10",
  Running: "blue",
  PartiallyFailed: "orange",
  Created: "grey"
}[status] || "grey");

const isRetryable = (status) => ["Failed", "PermanentlyFailed"].includes(status);
const formatDate = (d) => (d ? date.formatDate(d, "YYYY-MM-DD HH:mm") : "—");

// ---- Jobs ----
const jobColumns = [
  { name: "status", label: "Status", field: "status", align: "left", sortable: true },
  { name: "interfaceName", label: "Interface", field: "interfaceName", align: "left", sortable: true },
  { name: "sourceSystem", label: "Source", field: "sourceSystem", align: "left" },
  { name: "targetSystem", label: "Target", field: "targetSystem", align: "left" },
  { name: "createdDate", label: "Created", field: (r) => formatDate(r.createdDate), align: "left", sortable: true },
  { name: "processedDate", label: "Processed", field: (r) => formatDate(r.processedDate), align: "left" },
  { name: "actions", label: "", field: "actions", align: "right" }
];
const jobs = ref([]);
const loadingJobs = ref(false);
const jobsTotal = ref(0);
const jobsPagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });
const filters = reactive({ status: null, interfaceName: "", fromDate: null, toDate: null });

const filterChips = computed(() => {
  const chips = [];
  if (filters.status) chips.push({ key: "status", label: `Status: ${filters.status}` });
  if (filters.interfaceName) chips.push({ key: "interfaceName", label: `Interface: ${filters.interfaceName}` });
  if (filters.fromDate) chips.push({ key: "fromDate", label: `From: ${filters.fromDate}` });
  if (filters.toDate) chips.push({ key: "toDate", label: `To: ${filters.toDate}` });
  return chips;
});
const removeFilter = (key) => { filters[key] = key === "interfaceName" ? "" : null; loadJobs(); };
const clearFilters = () => { filters.status = null; filters.interfaceName = ""; filters.fromDate = null; filters.toDate = null; loadJobs(); };

const loadJobs = async () => {
  loadingJobs.value = true;
  try {
    const resp = await jobApi.list({
      page: jobsPagination.value.page,
      limit: jobsPagination.value.rowsPerPage,
      status: filters.status || undefined,
      interfaceName: filters.interfaceName || undefined,
      fromDate: filters.fromDate || undefined,
      toDate: filters.toDate || undefined
    });
    jobs.value = resp?.data || [];
    jobsTotal.value = resp?.meta?.totalRecords ?? jobs.value.length;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingJobs.value = false;
  }
};
const onJobsRequest = (pag) => { jobsPagination.value = { ...jobsPagination.value, ...pag }; loadJobs(); };

// ---- Retry queue ----
const retryColumns = [
  { name: "jobId", label: "Job ID", field: "jobId", align: "left" },
  { name: "retryCount", label: "Retry count", field: "retryCount", align: "left" },
  { name: "nextRetryDate", label: "Next retry", field: (r) => formatDate(r.nextRetryDate), align: "left" },
  { name: "status", label: "Status", field: "status", align: "left" },
  { name: "actions", label: "", field: "actions", align: "right" }
];
const retries = ref([]);
const loadingRetries = ref(false);
const retriesTotal = ref(0);
const retriesPagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });

const loadRetries = async () => {
  loadingRetries.value = true;
  try {
    const resp = await jobApi.retries({ page: retriesPagination.value.page, limit: retriesPagination.value.rowsPerPage });
    retries.value = resp?.data || [];
    retriesTotal.value = resp?.meta?.totalRecords ?? retries.value.length;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingRetries.value = false;
  }
};
const onRetriesRequest = (pag) => { retriesPagination.value = { ...retriesPagination.value, ...pag }; loadRetries(); };

const retry = async (jobId) => {
  const ok = await confirm({ title: "Retry job", message: "Re-enqueue this job for processing?", confirmLabel: "Retry" });
  if (!ok) return;
  try {
    await jobApi.retry(jobId);
    notify.success("Job re-enqueued.");
    loadJobs();
    loadRetries();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Detail ----
const detailOpen = ref(false);
const detailFields = ref([]);
const detailLogs = ref([]);
const openDetail = async (row) => {
  detailFields.value = [
    { label: "Job ID", value: row.jobId },
    { label: "Interface", value: row.interfaceName },
    { label: "Status", value: row.status },
    { label: "Source", value: row.sourceSystem },
    { label: "Target", value: row.targetSystem },
    { label: "Created", value: formatDate(row.createdDate) },
    { label: "Processed", value: formatDate(row.processedDate) }
  ];
  detailLogs.value = [];
  detailOpen.value = true;
  try {
    const resp = await logApi.list({ jobId: row.jobId, page: 1, limit: 100 });
    detailLogs.value = resp?.data || [];
  } catch {
    // non-fatal
  }
};

// ---- Trigger import ----
const triggerOpen = ref(false);
const triggering = ref(false);
const credBanner = ref(false);
const triggerFlow = ref("expenses");
const flowOptions = [
  { label: "Expense Reports", value: "expenses" },
  { label: "Vendor Invoices", value: "invoices" },
  { label: "Vendor Payments", value: "payments" }
];
const openTrigger = () => { credBanner.value = false; triggerFlow.value = "expenses"; triggerOpen.value = true; };

const submitTrigger = async () => {
  const ok = await confirm({ title: "Trigger import", message: "Queue this import for the active tenant?", confirmLabel: "Queue" });
  if (!ok) return;
  triggering.value = true;
  credBanner.value = false;
  try {
    const fn = { expenses: jobApi.importExpenses, invoices: jobApi.importInvoices, payments: jobApi.importPayments }[triggerFlow.value];
    const resp = await fn();
    const jobId = resp?.data?.jobId;
    notify.success(jobId ? `Import queued (job ${jobId}).` : "Import queued.");
    triggerOpen.value = false;
    loadJobs();
  } catch (err) {
    if (getApiErrorCode(err) === ApiErrorCodes.CredentialsNotConfigured) {
      credBanner.value = true;
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    triggering.value = false;
  }
};

const onTenantSwitched = () => { loadJobs(); loadRetries(); };
onMounted(() => {
  loadJobs();
  loadRetries();
  window.addEventListener("tenant-switched", onTenantSwitched);
});
onBeforeUnmount(() => window.removeEventListener("tenant-switched", onTenantSwitched));
</script>
