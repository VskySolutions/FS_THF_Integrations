<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Mapping Configuration' }]"
      show-filters
      :filter-count="filterChips.length"
      show-back
      @filters="filterOpen = true"
      @back="$router.back()"
    >
      <template #actions>
        <app-select
          v-if="canChooseTenant" v-model="selectedTenantId" :options="tenantOptions" label="Tenant"
          :loading="loadingTenants" :clearable="false" style="min-width: 220px;"
        />
      </template>
    </app-list-header>

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
    </app-filter-drawer>

    <q-banner v-if="!tenantId" dense class="bg-orange-1 text-orange-9 q-mb-md">
      <template #avatar><q-icon name="o_warning" color="orange" /></template>
      No active tenant selected.
    </q-banner>

    <app-data-table
      page-key="flow-mappings"
      row-key="interfaceName"
      title="Field mappings by flow"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :total-records="filteredRows.length"
      :pagination="pagination"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-pair="cell">
        <q-td :props="cell">
          <q-badge color="blue-grey" :label="cell.row.sourceSystem" />
          <q-icon name="o_arrow_forward" size="14px" class="text-grey-7 q-mx-xs" />
          <q-badge color="primary" :label="cell.row.destinationSystem" />
        </q-td>
      </template>

      <template #body-cell-fieldCount="cell">
        <q-td :props="cell">
          <q-badge :color="cell.value ? 'primary' : 'grey-4'" :text-color="cell.value ? 'white' : 'grey-8'">
            {{ cell.value }} field{{ cell.value === 1 ? "" : "s" }}
          </q-badge>
        </q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_edit" @click="openEdit(cell.row)">
            <q-tooltip>Edit mappings</q-tooltip>
          </q-btn>
          <q-btn v-if="cell.row.fieldCount" flat round dense color="negative" icon="o_delete" @click="clearFlow(cell.row)">
            <q-tooltip>Clear all fields</q-tooltip>
          </q-btn>
        </q-td>
      </template>
    </app-data-table>

    <!-- Edit a flow's field mappings -->
    <app-form-drawer
      v-model="formOpen" :title="`${form.flowLabel} mappings`" save-label="Save"
      :saving="saving" @submit="submitForm" @cancel="resetForm"
    >
      <div class="row items-center q-gutter-xs q-mb-md">
        <q-chip dense square color="indigo-1" text-color="indigo-9">{{ form.flowLabel }}</q-chip>
        <q-badge color="blue-grey" :label="form.sourceSystem" />
        <q-icon name="o_arrow_forward" size="16px" class="text-grey-7" />
        <q-badge color="primary" :label="form.destinationSystem" />
      </div>

      <q-expansion-item
        icon="o_lightbulb" label="Mapping examples" dense-toggle
        class="mapping-examples q-mb-md bg-blue-grey-1 rounded-borders"
      >
        <q-markup-table flat dense separator="cell" class="examples-table">
          <thead>
            <tr>
              <th class="text-left">Pattern</th>
              <th class="text-left">Source field</th>
              <th class="text-left">Destination field</th>
              <th class="text-left">Rule</th>
              <th class="text-left">Result</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(ex, i) in mappingExamples" :key="i">
              <td class="text-left">{{ ex.pattern }}</td>
              <td class="text-left"><code>{{ ex.source }}</code></td>
              <td class="text-left"><code>{{ ex.dest }}</code></td>
              <td class="text-left"><code>{{ ex.rule }}</code></td>
              <td class="text-left">{{ ex.result }}</td>
            </tr>
          </tbody>
        </q-markup-table>
      </q-expansion-item>

      <q-markup-table flat bordered dense separator="cell" class="fields-table q-mb-md">
        <thead>
          <tr>
            <th class="text-left">Source field *</th>
            <th class="text-left">Destination field *</th>
            <th class="text-left">Transformation rule</th>
            <th style="width: 40px;" />
          </tr>
        </thead>
        <tbody>
          <tr v-for="(r, i) in form.fields" :key="r.key">
            <td><q-input v-model="r.sourceField" dense outlined hide-bottom-space placeholder="e.g. ReportId" /></td>
            <td><q-input v-model="r.destinationField" dense outlined hide-bottom-space placeholder="e.g. ReportNumber" /></td>
            <td><q-input v-model="r.transformationRule" dense outlined hide-bottom-space placeholder="optional" /></td>
            <td class="text-center">
              <q-btn flat round dense icon="o_delete" color="negative" :disable="form.fields.length === 1" @click="removeFieldRow(i)">
                <q-tooltip>Remove row</q-tooltip>
              </q-btn>
            </td>
          </tr>
        </tbody>
      </q-markup-table>
      <q-btn flat no-caps color="primary" icon="o_add" label="Add Row" @click="addFieldRow" />
    </app-form-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from "vue";
import { flowMappingApi, getApiErrorMessage } from "services/api";
import { useTenantStore } from "stores/tenant";
import { useTenantOptions } from "composables/useTenantOptions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";

// Worked, end-to-end mapping examples shown in the collapsible panel in the form.
const mappingExamples = [
  { pattern: "Simple key → key", source: "ReportId", dest: "ReportNumber", rule: "—", result: "Copies the value as-is" },
  { pattern: "Rename + value lookup", source: "CurrencyCode", dest: "Currency", rule: "lookup:USD=US Dollar;EUR=Euro;default=Other", result: "Maps a code to a label" },
  { pattern: "Nested object", source: "Employee.CostCenter", dest: "DepartmentCode", rule: "—", result: "Reads a nested value by dot path" },
  { pattern: "Concatenate fields", source: "FirstName", dest: "FullName", rule: "concat:FirstName,' ',LastName", result: "Joins fields and 'literals'" },
  { pattern: "Reformat date", source: "SubmitDate", dest: "PostingDate", rule: "date:yyyy-MM-dd|dd/MM/yyyy", result: "Changes the date format" },
  { pattern: "Array element", source: "Lines[0].Amount", dest: "FirstLineAmount", rule: "—", result: "One array item by index" }
];

const notify = useNotify();
const { confirm } = useConfirm();
const tenantStore = useTenantStore();
const { canChooseTenant, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();

// Super admins target any tenant via the dropdown; other roles use their active tenant.
const selectedTenantId = ref(null);
const tenantId = computed(() => (canChooseTenant.value && selectedTenantId.value ? selectedTenantId.value : tenantStore.activeTenantId));

const fmt = useDateFormat();
const columns = [
  { name: "flowLabel", label: "Flow", field: "flowLabel", align: "left", sortable: true, default: true },
  { name: "pair", label: "Systems", field: "sourceSystem", align: "left", default: true },
  { name: "fieldCount", label: "Fields", field: "fieldCount", align: "left", sortable: true, default: true, filterable: false },
  { name: "updatedOnUtc", label: "Updated", field: (r) => (r.updatedOnUtc ? fmt.formatDateTime(r.updatedOnUtc) : "—"), align: "left", sortable: true, default: true, filterable: false },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

const { rows, loading, pagination, load, onRequest } = useListTable({
  fetcher: () => {
    if (!tenantId.value) return Promise.resolve({ data: [], total: 0 });
    return flowMappingApi.list(tenantId.value).then((list) => ({ data: list || [], total: (list || []).length }));
  },
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Client-side column filters (the list loads all flow mappings); badge/count standard via AppListHeader.
const filterOpen = ref(false);
const { filters, filterableColumns, filteredRows, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: false });

watch(tenantId, () => { pagination.value.page = 1; load(); });

onMounted(() => {
  if (canChooseTenant.value) {
    loadTenants();
    selectedTenantId.value = tenantStore.activeTenantId;
  }
});

// ---- Edit a flow's field set ----
const formOpen = ref(false);
const saving = ref(false);
let fieldSeq = 0;
const newFieldRow = () => ({ key: ++fieldSeq, sourceField: "", destinationField: "", transformationRule: "" });
const form = reactive({ interfaceName: "", flowLabel: "", sourceSystem: "", destinationSystem: "", fields: [newFieldRow()] });

const resetForm = () => {
  Object.assign(form, { interfaceName: "", flowLabel: "", sourceSystem: "", destinationSystem: "", fields: [newFieldRow()] });
};
const addFieldRow = () => { form.fields.push(newFieldRow()); };
const removeFieldRow = (i) => { form.fields.splice(i, 1); };

const openEdit = async (row) => {
  resetForm();
  try {
    const detail = await flowMappingApi.get(tenantId.value, row.interfaceName);
    Object.assign(form, {
      interfaceName: detail.interfaceName,
      flowLabel: detail.flowLabel,
      sourceSystem: detail.sourceSystem,
      destinationSystem: detail.destinationSystem,
      fields: (detail.fields && detail.fields.length)
        ? detail.fields.map((f) => ({ key: ++fieldSeq, sourceField: f.sourceField, destinationField: f.destinationField, transformationRule: f.transformationRule || "" }))
        : [newFieldRow()]
    });
    formOpen.value = true;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const submitForm = async ({ clearDraft } = {}) => {
  // Drop blank rows; every remaining row must be complete; no duplicate source fields.
  const filled = form.fields.filter((r) => r.sourceField.trim() || r.destinationField.trim() || r.transformationRule.trim());
  if (filled.some((r) => !r.sourceField.trim() || !r.destinationField.trim())) {
    notify.error("Fill both source and destination field on every row.");
    return;
  }
  const seen = new Set();
  for (const r of filled) {
    const key = r.sourceField.trim().toLowerCase();
    if (seen.has(key)) { notify.error(`Duplicate source field "${r.sourceField.trim()}".`); return; }
    seen.add(key);
  }

  const fields = filled.map((r) => ({
    sourceField: r.sourceField.trim(),
    destinationField: r.destinationField.trim(),
    transformationRule: r.transformationRule.trim() || null
  }));

  saving.value = true;
  try {
    await flowMappingApi.save(tenantId.value, form.interfaceName, fields);
    clearDraft?.();
    formOpen.value = false;
    resetForm();
    notify.success("Mappings saved.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

const clearFlow = async (row) => {
  const ok = await confirm({
    title: "Clear field mappings",
    message: `Remove all ${row.fieldCount} field mapping(s) for "${row.flowLabel}"? The flow will fall back to default field mappings.`,
    confirmLabel: "Clear",
    type: "danger"
  });
  if (!ok) return;
  try {
    await flowMappingApi.clear(tenantId.value, row.interfaceName);
    notify.success("Field mappings cleared.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
</script>

<style scoped>
.examples-table {
  font-size: 12px;
}
.examples-table code {
  background: #ffffff;
  padding: 1px 4px;
  border-radius: 4px;
  white-space: nowrap;
}
.examples-table th,
.examples-table td {
  vertical-align: top;
}

/* Editable field-mapping table inside the drawer. */
.fields-table th {
  font-weight: 600;
  white-space: nowrap;
}
.fields-table td {
  padding: 4px 6px;
  vertical-align: top;
}
.fields-table :deep(.q-field) {
  min-width: 130px;
}
</style>
