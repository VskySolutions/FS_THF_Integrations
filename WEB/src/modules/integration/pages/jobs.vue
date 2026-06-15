<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Integration Jobs' }]"
      :show-filters="tab === 'jobs'"
      :filter-count="filterChips.length"
      show-add
      add-label="Trigger Import"
      add-icon="o_play_arrow"
      show-back
      @filters="filterOpen = true"
      @add="openTrigger"
      @back="$router.back()"
    />

    <q-tabs v-model="tab" align="left" class="text-primary q-mb-sm" dense narrow-indicator>
      <q-tab name="jobs" label="Jobs" no-caps />
      <q-tab name="retries" label="Retry Queue" no-caps />
      <q-tab v-if="canSchedule" name="schedules" label="Schedules" no-caps />
    </q-tabs>
    <q-separator class="q-mb-md" />

    <q-tab-panels v-model="tab" animated>
      <!-- Jobs -->
      <q-tab-panel name="jobs" class="q-pa-none">
        <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
          <app-select v-model="filters.status" :options="statusOptions" label="Status" />
          <q-input v-model="filters.interfaceName" outlined dense stack-label hide-bottom-space clearable label="Interface name" />
          <app-date-picker v-model="filters.fromDate" label="From date" :max-date="filters.toDate" />
          <app-date-picker v-model="filters.toDate" label="To date" :min-date="filters.fromDate" />
        </app-filter-drawer>

        <app-data-table
          page-key="jobs"
          row-key="jobId"
          title="Jobs"
          :rows="jobs"
          :columns="jobColumns"
          :loading="loadingJobs"
          :total-records="jobsTotal"
          :pagination="jobsPagination"
          selectable
          @request="onJobsRequest"
          @refresh="loadJobs"
          @update:selected="selectedJobs = $event"
        >
          <template #bulk-actions="{ selected: sel }">
            <q-btn flat dense no-caps color="primary" icon="o_replay" label="Retry" @click="bulkRetry(sel.filter((r) => isRetryable(r.status)))" />
            <q-btn v-if="has(Permissions.JobsDelete)" flat dense no-caps color="negative" icon="o_delete" label="Delete" @click="bulkDelete(sel.filter((r) => isDeletable(r.status)))" />
          </template>

          <template #body-cell-status="cell">
            <q-td :props="cell"><q-badge :color="statusColor(cell.value)">{{ cell.value }}</q-badge></q-td>
          </template>
          <template #body-cell-direction="cell">
            <q-td :props="cell">
              <q-badge outline :color="directionColor(cell.value)">
                <q-icon :name="cell.value === 'Outbound' ? 'o_north_east' : 'o_south_west'" size="14px" class="q-mr-xs" />
                {{ cell.value || "—" }}
              </q-badge>
              <q-tooltip v-if="cell.row.sourceSystem">{{ cell.row.sourceSystem }} → {{ cell.row.targetSystem }}</q-tooltip>
            </q-td>
          </template>
          <template #body-cell-actions="cell">
            <q-td :props="cell" class="text-right">
              <q-btn flat round dense color="primary" icon="o_visibility" @click="openDetail(cell.row)">
                <q-tooltip>View detail</q-tooltip>
              </q-btn>
              <q-btn
                flat round dense color="primary" icon="o_replay"
                :disable="!isRetryable(cell.row.status)" @click="retry(cell.row.jobId)"
              >
                <q-tooltip>{{ isRetryable(cell.row.status) ? "Retry" : "Job cannot be retried in this state" }}</q-tooltip>
              </q-btn>
              <q-btn
                v-if="has(Permissions.JobsDelete)" flat round dense color="negative" icon="o_delete"
                :disable="!isDeletable(cell.row.status)" @click="deleteJob(cell.row)"
              >
                <q-tooltip>{{ isDeletable(cell.row.status) ? "Delete" : "Queued or running jobs can't be deleted" }}</q-tooltip>
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
          selectable
          @request="onRetriesRequest"
          @refresh="loadRetries"
          @update:selected="selectedRetries = $event"
        >
          <template #bulk-actions="{ selected: sel }">
            <q-btn flat dense no-caps color="primary" icon="o_replay" label="Retry" @click="bulkRetry(sel)" />
          </template>

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

      <!-- Schedules -->
      <q-tab-panel v-if="canSchedule" name="schedules" class="q-pa-none">
        <app-select
          v-if="canChooseScheduleTenant" v-model="scheduleTenantId" :options="scheduleTenantOptions"
          label="Tenant" :loading="loadingScheduleTenants" :clearable="false" class="q-mt-sm" style="max-width: 360px;"
        />
        <q-card flat bordered class="q-mt-sm">
          <q-list separator>
            <q-item v-for="s in schedules" :key="s.jobName">
              <q-item-section>
                <q-item-label class="text-weight-medium">{{ s.displayName }}</q-item-label>
                <q-item-label caption>
                  <template v-if="s.cronExpression">
                    <code>{{ s.cronExpression }}</code>
                    <span v-if="cronHint(s.cronExpression)"> · {{ cronHint(s.cronExpression) }}</span>
                  </template>
                  <span v-else class="text-grey-6">Not scheduled</span>
                </q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-badge :color="s.isActive ? 'positive' : 'grey'">{{ s.isActive ? "Active" : "Paused" }}</q-badge>
              </q-item-section>
              <q-item-section side>
                <q-btn flat round dense icon="o_edit" @click="openScheduleEdit(s)"><q-tooltip>Edit schedule</q-tooltip></q-btn>
              </q-item-section>
            </q-item>
            <q-item v-if="!schedules.length">
              <q-item-section class="text-grey-6">No schedules.</q-item-section>
            </q-item>
          </q-list>
        </q-card>
        <div class="text-caption text-grey-7 q-pa-sm">
          Schedules run in <strong>UTC</strong> and apply to all active tenants. Changes take effect within ~1 minute (no restart).
        </div>
      </q-tab-panel>
    </q-tab-panels>

    <!-- Edit schedule -->
    <app-form-drawer
      v-model="scheduleOpen" :title="`Schedule — ${scheduleForm.displayName}`" save-label="Save schedule"
      :saving="scheduleSaving" @submit="submitSchedule" @cancel="scheduleOpen = false"
    >
      <q-form ref="scheduleFormRef" greedy>
        <q-input
          v-model="scheduleForm.cronExpression" outlined stack-label hide-bottom-space label="Cron expression (UTC) *" class="q-mb-xs"
          :rules="[(v) => isValidCron(v) || 'Use a 5-field cron, e.g. 0 2 * * *']"
        />
        <div class="text-caption text-grey-7 q-mb-md">{{ cronHint(scheduleForm.cronExpression) || "minute hour day month weekday — evaluated in UTC" }}</div>
        <q-toggle v-model="scheduleForm.isActive" label="Active" />
        <q-expansion-item dense icon="o_lightbulb" label="Common schedules" class="q-mt-md bg-blue-grey-1 rounded-borders">
          <q-list dense>
            <q-item v-for="ex in cronExamples" :key="ex.cron" clickable @click="scheduleForm.cronExpression = ex.cron">
              <q-item-section>
                <q-item-label><code>{{ ex.cron }}</code></q-item-label>
                <q-item-label caption>{{ ex.desc }}</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </q-expansion-item>
      </q-form>
    </app-form-drawer>

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
      <app-select
        v-if="canChooseTriggerTenant" v-model="triggerTenantId" :options="triggerTenantOptions" label="Tenant *"
        class="q-mb-md" :loading="loadingTriggerTenants" :clearable="false"
        :error="triggerTouched && !triggerTenantId" error-message="Select a tenant."
      />

      <app-select v-model="triggerFlow" :options="flowOptions" label="Flow *" :clearable="false" />

      <div class="q-mt-md">
        <div class="text-caption text-grey-7 q-mb-xs">This import will run</div>
        <div class="row items-center q-gutter-xs">
          <q-badge color="blue-grey" label="Concur" />
          <q-icon name="o_arrow_forward" size="18px" class="text-grey-7" />
          <q-badge color="primary" label="Maconomy" />
          <q-chip dense square color="grey-3" text-color="grey-8">Inbound</q-chip>
        </div>
        <div class="text-caption text-grey-6 q-mt-xs">The flow's field mappings (configured under Mapping Configuration) are applied automatically.</div>
      </div>
    </app-form-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted, onBeforeUnmount } from "vue";
import { useDateFormat } from "composables/useDateFormat";
import { useTenantOptions } from "composables/useTenantOptions";
import { jobApi, logApi, scheduleApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useTenantStore } from "stores/tenant";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppViewDrawer from "components/common/AppViewDrawer.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppDatePicker from "components/common/AppDatePicker.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppListHeader from "components/common/AppListHeader.vue";

const notify = useNotify();
const { confirm } = useConfirm();
// Only super/platform admins see jobs across tenants — and so get the Tenant column.
const { canChooseTenant } = useTenantOptions();

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
// Queued/running jobs are in-flight and can't be deleted (would orphan a background execution).
const isDeletable = (status) => !["Created", "Running"].includes(status);
const directionColor = (direction) => (direction === "Outbound" ? "deep-purple" : "teal");
const { formatDateTime: formatDate } = useDateFormat();

// ---- Jobs ----
const jobColumns = computed(() => [
  ...(canChooseTenant.value
    ? [{ name: "tenantName", label: "Tenant", field: (r) => r.tenantName || "—", align: "left", sortable: true, default: true }]
    : []),
  { name: "status", label: "Status", field: "status", align: "left", sortable: true, default: true },
  { name: "interfaceName", label: "Interface", field: "interfaceName", align: "left", sortable: true, default: true },
  { name: "flowLabel", label: "Flow", field: (r) => r.flowLabel || r.interfaceName, align: "left", sortable: true, default: true },
  { name: "direction", label: "Direction", field: "direction", align: "left", sortable: true, default: true },
  { name: "sourceSystem", label: "Source", field: "sourceSystem", align: "left", default: true },
  { name: "targetSystem", label: "Target", field: "targetSystem", align: "left", default: true },
  { name: "createdDate", label: "Created", field: (r) => formatDate(r.createdDate), align: "left", sortable: true, default: true },
  { name: "processedDate", label: "Processed", field: (r) => formatDate(r.processedDate), align: "left" },
  { name: "createdBy", label: "Created By", field: "createdBy", align: "left", sortable: true },
  { name: "updatedBy", label: "Updated By", field: "updatedBy", align: "left", sortable: true },
  { name: "updatedOnUtc", label: "Updated", field: (r) => formatDate(r.updatedOnUtc), align: "left", sortable: true },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);
const jobs = ref([]);
const loadingJobs = ref(false);
const jobsTotal = ref(0);
const jobsPagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });
const filterOpen = ref(false);
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
  { name: "jobId", label: "Job ID", field: "jobId", align: "left", sortable: true },
  { name: "retryCount", label: "Retry count", field: "retryCount", align: "left", sortable: true },
  { name: "nextRetryDate", label: "Next retry", field: (r) => formatDate(r.nextRetryDate), align: "left", sortable: true },
  { name: "status", label: "Status", field: "status", align: "left", sortable: true },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];
const retries = ref([]);
const loadingRetries = ref(false);
const retriesTotal = ref(0);
const retriesPagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });

// Multi-select state for both tables.
const selectedJobs = ref([]);
const selectedRetries = ref([]);

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

const bulkRetry = async (sel) => {
  const ids = (sel || []).map((r) => r.jobId);
  if (!ids.length) {
    notify.error("No retryable jobs selected.");
    return;
  }
  const ok = await confirm({ title: "Retry jobs", message: `Re-enqueue ${ids.length} job(s) for processing?`, confirmLabel: "Retry" });
  if (!ok) return;
  try {
    await Promise.all(ids.map((id) => jobApi.retry(id)));
    notify.success("Jobs re-enqueued.");
    selectedJobs.value = [];
    selectedRetries.value = [];
    loadJobs();
    loadRetries();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const deleteJob = async (row) => {
  const ok = await confirm({
    title: "Delete job",
    message: `Delete this ${row.interfaceName} job? It will be removed from the job history.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await jobApi.remove(row.jobId);
    notify.success("Job deleted.");
    loadJobs();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const bulkDelete = async (sel) => {
  const rows = sel || [];
  if (!rows.length) {
    notify.error("No deletable jobs selected (queued/running jobs can't be deleted).");
    return;
  }
  const ok = await confirm({
    title: "Delete jobs",
    message: `Delete ${rows.length} job(s) from history? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await Promise.all(rows.map((r) => jobApi.remove(r.jobId)));
    notify.success("Jobs deleted.");
    selectedJobs.value = [];
    loadJobs();
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
    ...(canChooseTenant.value ? [{ label: "Tenant", value: row.tenantName || "—" }] : []),
    { label: "Interface", value: row.interfaceName },
    { label: "Status", value: row.status },
    { label: "Direction", value: row.direction },
    { label: "Flow", value: row.flowLabel || row.interfaceName },
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

// ---- Schedules (per tenant) ----
const { has } = usePermissions();
const canSchedule = computed(() => has(Permissions.JobsSchedule));
// Super admins pick which tenant's schedules to manage; others are scoped to their active tenant.
const {
  canChooseTenant: canChooseScheduleTenant,
  tenantOptions: scheduleTenantOptions,
  loadingTenants: loadingScheduleTenants,
  loadTenants: loadScheduleTenants
} = useTenantOptions();
const scheduleTenantId = ref(null);
const schedules = ref([]);
const scheduleOpen = ref(false);
const scheduleSaving = ref(false);
const scheduleFormRef = ref(null);
const scheduleForm = reactive({ jobName: "", displayName: "", cronExpression: "", isActive: true });

const cronExamples = [
  { cron: "0 2 * * *", desc: "Daily at 02:00 UTC" },
  { cron: "30 23 * * *", desc: "Daily at 23:30 UTC" },
  { cron: "0 6 * * 1-5", desc: "Weekdays at 06:00 UTC" },
  { cron: "0 * * * *", desc: "Every hour" },
  { cron: "*/15 * * * *", desc: "Every 15 minutes" },
  { cron: "0 0 1 * *", desc: "1st of month, 00:00 UTC" }
];
const cronHint = (cron) => cronExamples.find((e) => e.cron === (cron || "").trim())?.desc || "";
const isValidCron = (v) => !!v && v.trim().split(/\s+/).length === 5;

const loadSchedules = async () => {
  try {
    schedules.value = await scheduleApi.list(scheduleTenantId.value || undefined);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
watch(scheduleTenantId, loadSchedules);

const openScheduleEdit = (s) => {
  scheduleForm.jobName = s.jobName;
  scheduleForm.displayName = s.displayName;
  scheduleForm.cronExpression = s.cronExpression || "";
  scheduleForm.isActive = s.isActive;
  scheduleOpen.value = true;
};

const submitSchedule = async ({ clearDraft } = {}) => {
  if (!(await scheduleFormRef.value?.validate())) return;
  scheduleSaving.value = true;
  try {
    await scheduleApi.update(scheduleForm.jobName, { cronExpression: scheduleForm.cronExpression.trim(), isActive: scheduleForm.isActive }, scheduleTenantId.value || undefined);
    notify.success("Schedule updated.");
    clearDraft?.();
    scheduleOpen.value = false;
    loadSchedules();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    scheduleSaving.value = false;
  }
};

// ---- Trigger import ----
const tenantStore = useTenantStore();
const triggerOpen = ref(false);
const triggering = ref(false);
const credBanner = ref(false);
const triggerTouched = ref(false);
// Super admins choose which tenant to import for; others run for their active tenant.
const {
  canChooseTenant: canChooseTriggerTenant,
  tenantOptions: triggerTenantOptions,
  loadingTenants: loadingTriggerTenants,
  loadTenants: loadTriggerTenants
} = useTenantOptions();
const triggerTenantId = ref(null);
// The user picks a flow; the server resolves that (tenant, flow) field set automatically.
const triggerFlow = ref("expenses");
const flowOptions = [
  { label: "Expense Reports", value: "expenses" },
  { label: "Vendor Invoices", value: "invoices" },
  { label: "Vendor Payments", value: "payments" }
];

const openTrigger = () => {
  credBanner.value = false;
  triggerTouched.value = false;
  triggerFlow.value = "expenses";
  if (canChooseTriggerTenant.value) {
    loadTriggerTenants();
    triggerTenantId.value = tenantStore.activeTenantId;
  }
  triggerOpen.value = true;
};

const submitTrigger = async () => {
  triggerTouched.value = true;
  if (canChooseTriggerTenant.value && !triggerTenantId.value) {
    notify.error("Select a tenant.");
    return;
  }
  const fn = { expenses: jobApi.importExpenses, invoices: jobApi.importInvoices, payments: jobApi.importPayments }[triggerFlow.value];
  const ok = await confirm({ title: "Trigger import", message: "Queue this import for the selected tenant?", confirmLabel: "Queue" });
  if (!ok) return;
  triggering.value = true;
  credBanner.value = false;
  try {
    const resp = await fn(canChooseTriggerTenant.value ? triggerTenantId.value : undefined);
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
  if (canSchedule.value) {
    if (canChooseScheduleTenant.value) loadScheduleTenants();
    loadSchedules();
  }
  window.addEventListener("tenant-switched", onTenantSwitched);
});
onBeforeUnmount(() => window.removeEventListener("tenant-switched", onTenantSwitched));
</script>
