<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Logs' }]"
      show-filters
      :filter-count="filterChips.length"
      show-back
      @filters="filterOpen = true"
      @back="$router.back()"
    />

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <q-input v-model="filters.jobId" outlined clearable label="Job ID" class="q-mb-md" />
      <app-select v-model="filters.status" :options="statusOptions" label="Level" class="q-mb-md" />
      <app-date-picker v-model="filters.fromDate" label="From date" :max-date="filters.toDate" class="q-mb-md" />
      <app-date-picker v-model="filters.toDate" label="To date" :min-date="filters.fromDate" />
    </app-filter-drawer>

    <app-data-table
      page-key="logs"
      row-key="id"
      title="Logs"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="total"
      :pagination="pagination"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-level="cell">
        <q-td :props="cell"><q-badge :color="levelColor(cell.value)">{{ cell.value }}</q-badge></q-td>
      </template>
      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense icon="o_visibility" @click="openDetail(cell.row)" />
        </q-td>
      </template>
    </app-data-table>

    <app-view-drawer v-model="detailOpen" title="Log entry" :fields="detailFields">
      <q-banner v-if="detail && isError(detail.level)" dense class="bg-red-1 text-negative q-mt-sm">
        <template #avatar><q-icon name="o_error" color="negative" /></template>
        {{ detail.message }}
      </q-banner>
    </app-view-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onBeforeUnmount } from "vue";
import { date } from "quasar";
import { logApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppViewDrawer from "components/common/AppViewDrawer.vue";
import AppDatePicker from "components/common/AppDatePicker.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppListHeader from "components/common/AppListHeader.vue";

const notify = useNotify();

const statusOptions = ["Information", "Warning", "Error"].map((s) => ({ label: s, value: s }));
const formatDate = (d) => (d ? date.formatDate(d, "YYYY-MM-DD HH:mm") : "—");
const isError = (level) => (level || "").toLowerCase().startsWith("err");
const levelColor = (level) => {
  const l = (level || "").toLowerCase();
  if (l.startsWith("err")) return "negative";
  if (l.startsWith("warn")) return "orange";
  return "grey";
};

const columns = [
  { name: "level", label: "Level", field: "level", align: "left" },
  { name: "jobId", label: "Job ID", field: "jobId", align: "left" },
  { name: "message", label: "Message", field: "message", align: "left" },
  { name: "createdDate", label: "Time", field: (r) => formatDate(r.createdDate), align: "left", sortable: true },
  { name: "actions", label: "", field: "actions", align: "right" }
];

const rows = ref([]);
const loading = ref(false);
const total = ref(0);
const pagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });
const filterOpen = ref(false);
const filters = reactive({ jobId: "", status: null, fromDate: null, toDate: null });

const filterChips = computed(() => {
  const chips = [];
  if (filters.jobId) chips.push({ key: "jobId", label: `Job: ${filters.jobId}` });
  if (filters.status) chips.push({ key: "status", label: `Level: ${filters.status}` });
  if (filters.fromDate) chips.push({ key: "fromDate", label: `From: ${filters.fromDate}` });
  if (filters.toDate) chips.push({ key: "toDate", label: `To: ${filters.toDate}` });
  return chips;
});
const removeFilter = (key) => { filters[key] = key === "jobId" ? "" : null; load(); };
const clearFilters = () => { filters.jobId = ""; filters.status = null; filters.fromDate = null; filters.toDate = null; load(); };

const load = async () => {
  loading.value = true;
  try {
    const resp = await logApi.list({
      page: pagination.value.page,
      limit: pagination.value.rowsPerPage,
      jobId: filters.jobId || undefined,
      status: filters.status || undefined,
      fromDate: filters.fromDate || undefined,
      toDate: filters.toDate || undefined
    });
    rows.value = resp?.data || [];
    total.value = resp?.meta?.totalRecords ?? rows.value.length;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};
const onRequest = (pag) => { pagination.value = { ...pagination.value, ...pag }; load(); };

const detailOpen = ref(false);
const detail = ref(null);
const detailFields = ref([]);
const openDetail = (row) => {
  detail.value = row;
  detailFields.value = [
    { label: "Level", value: row.level },
    { label: "Job ID", value: row.jobId },
    { label: "Time", value: formatDate(row.createdDate) },
    { label: "Message", value: row.message }
  ];
  detailOpen.value = true;
};

const onTenantSwitched = () => load();
onMounted(() => {
  load();
  window.addEventListener("tenant-switched", onTenantSwitched);
});
onBeforeUnmount(() => window.removeEventListener("tenant-switched", onTenantSwitched));
</script>
