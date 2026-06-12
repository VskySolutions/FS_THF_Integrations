<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Mapping Configuration' }]"
      :search="search"
      show-search
      search-placeholder="Search fields"
      show-filters
      :filter-count="filterChips.length"
      show-add
      add-label="Add Mapping"
      :add-disable="!tenantId"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @add="openCreate"
      @back="$router.back()"
    />

    <q-banner v-if="!tenantId" dense class="bg-orange-1 text-orange-9 q-mb-md">
      <template #avatar><q-icon name="o_warning" color="orange" /></template>
      No active tenant selected.
    </q-banner>

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-select v-if="canChooseTenant" v-model="selectedTenantId" :options="tenantOptions" label="Tenant" :loading="loadingTenants" :clearable="false" />
      <app-select v-model="filters.sourceSystem" :options="systemOptions" label="Source system" />
      <app-select v-model="filters.destinationSystem" :options="systemOptions" label="Destination system" />
    </app-filter-drawer>

    <app-data-table
      page-key="mappings"
      row-key="id"
      title="Field mappings"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="total"
      :pagination="pagination"
      selectable
      @request="onRequest"
      @refresh="load"
      @update:selected="selected = $event"
    >
      <template #bulk-actions="{ selected: sel }">
        <q-btn flat dense no-caps color="positive" label="Activate" @click="bulkSetActive(sel, true)" />
        <q-btn flat dense no-caps color="negative" label="Deactivate" @click="bulkSetActive(sel, false)" />
        <q-btn flat dense no-caps color="negative" icon="o_delete" label="Delete" @click="bulkRemove(sel)" />
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-lg text-grey-6">
          <q-icon name="o_swap_horiz" size="32px" class="q-mb-sm" />
          No custom mappings configured — default field mappings are in use
        </div>
      </template>

      <template #body-cell-isActive="cell">
        <q-td :props="cell"><q-badge :color="cell.value ? 'positive' : 'grey'">{{ cell.value ? "Active" : "Inactive" }}</q-badge></q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 150px;">
                <q-item clickable @click="openEdit(cell.row)">
                  <q-item-section avatar><q-icon name="o_edit" /></q-item-section>
                  <q-item-section>Edit</q-item-section>
                </q-item>
                <q-item clickable @click="toggleActive(cell.row)">
                  <q-item-section avatar><q-icon :name="cell.row.isActive ? 'o_block' : 'o_check_circle'" /></q-item-section>
                  <q-item-section>{{ cell.row.isActive ? "Deactivate" : "Activate" }}</q-item-section>
                </q-item>
                <q-separator />
                <q-item clickable class="text-negative" @click="remove(cell.row)">
                  <q-item-section avatar><q-icon name="o_delete" color="negative" /></q-item-section>
                  <q-item-section>Delete</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>
    </app-data-table>

    <app-form-drawer v-model="formOpen" :title="editing ? 'Edit Mapping' : 'Add Mapping'" :saving="saving" @submit="submitForm" @cancel="resetForm">
      <q-form ref="formRef" greedy>
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
          <div class="text-caption text-grey-7 q-pa-sm">
            Whole line-item arrays are mapped by built-in defaults today; dotted/indexed paths require the source connector to expose them.
          </div>
        </q-expansion-item>

        <app-select
          v-if="canChooseTenant" v-model="selectedTenantId" :options="tenantOptions" label="Tenant *"
          class="q-mb-md" :clearable="false" :loading="loadingTenants" :disable="editing"
        />
        <app-select v-model="form.sourceSystem" :options="systemOptions" label="Source system *" class="q-mb-md" :clearable="false" :disable="editing">
          <template #after><app-help-hint v-bind="help.sourceSystem" /></template>
        </app-select>
        <app-select v-model="form.destinationSystem" :options="systemOptions" label="Destination system *" class="q-mb-md" :clearable="false" :disable="editing">
          <template #after><app-help-hint v-bind="help.destinationSystem" /></template>
        </app-select>
        <q-input v-model="form.sourceField" outlined stack-label hide-bottom-space label="Source field *" class="q-mb-md" :disable="editing" :rules="[(v) => !!v || 'Required']">
          <template #after><app-help-hint v-bind="help.sourceField" /></template>
        </q-input>
        <q-input v-model="form.destinationField" outlined stack-label hide-bottom-space label="Destination field *" class="q-mb-md" :rules="[(v) => !!v || 'Required']">
          <template #after><app-help-hint v-bind="help.destinationField" /></template>
        </q-input>
        <q-input v-model="form.transformationRule" outlined stack-label hide-bottom-space label="Transformation rule (optional)" type="textarea" autogrow>
          <template #after><app-help-hint v-bind="help.transformationRule" /></template>
        </q-input>
      </q-form>
    </app-form-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from "vue";
import { debounce } from "quasar";
import { mappingApi, getApiErrorMessage } from "services/api";
import { useTenantStore } from "stores/tenant";
import { useTenantOptions } from "composables/useTenantOptions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useDateFormat } from "composables/useDateFormat";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppHelpHint from "components/common/AppHelpHint.vue";

// Field-level guidance shown via the help icons next to each mapping field.
const help = {
  sourceSystem: {
    title: "Source system",
    description: "The system the data is read from. Locked once the mapping is created.",
    examples: [
      { code: "Concur", desc: "Expense / invoice / payment source" },
      { code: "Maconomy", desc: "ERP (rare as an inbound source)" }
    ]
  },
  destinationSystem: {
    title: "Destination system",
    description: "The system the mapped value is written to. Locked once the mapping is created.",
    examples: [
      { code: "Maconomy", desc: "Typical inbound target" }
    ]
  },
  sourceField: {
    title: "Source field",
    description: "Exact field name from the source payload. Use dot notation for nested objects and [index] for arrays. Whole line-item arrays use built-in defaults today. Locked once created.",
    examples: [
      { code: "ReportId", desc: "Simple top-level field" },
      { code: "TotalAmount", desc: "Numeric header field" },
      { code: "Employee.CostCenter", desc: "Nested object (dot path)" },
      { code: "Customer.Address.City", desc: "Deeply nested object" },
      { code: "Lines[0].Amount", desc: "Array element by index" }
    ]
  },
  destinationField: {
    title: "Destination field",
    description: "Field in the destination schema that receives the value. Required destination fields must have an active mapping; anything unmapped falls back to the built-in default.",
    examples: [
      { code: "ProjectNumber", desc: "Maconomy field" },
      { code: "DepartmentCode", desc: "Maconomy field" }
    ]
  },
  transformationRule: {
    title: "Transformation rule",
    description: "Optional. Transforms the source value before it is written; leave blank to copy it unchanged. Grammar is kind:args.",
    examples: [
      { code: "(blank)", desc: "Copy the value through unchanged" },
      { code: "date:yyyy-MM-dd|dd/MM/yyyy", desc: "Reformat a date (source|target)" },
      { code: "lookup:USD=Dollar;EUR=Euro;default=Other", desc: "Map codes to values" },
      { code: "valuemap:Y=Yes;N=No", desc: "Alias of lookup" },
      { code: "concat:FirstName,' ',LastName", desc: "Join fields and 'literals'" }
    ]
  }
};

// Worked, end-to-end mapping examples shown in the collapsible panel at the top of the form.
const mappingExamples = [
  { pattern: "Simple key → key", source: "ReportId", dest: "ReportNumber", rule: "—", result: "Copies the value as-is" },
  { pattern: "Rename + value lookup", source: "CurrencyCode", dest: "Currency", rule: "lookup:USD=US Dollar;EUR=Euro;default=Other", result: "Maps a code to a label" },
  { pattern: "Nested object", source: "Employee.CostCenter", dest: "DepartmentCode", rule: "—", result: "Reads a nested value by dot path" },
  { pattern: "Deeply nested", source: "Customer.Address.City", dest: "City", rule: "—", result: "Any depth via dot path" },
  { pattern: "Concatenate fields", source: "FirstName", dest: "FullName", rule: "concat:FirstName,' ',LastName", result: "Joins fields and 'literals'" },
  { pattern: "Reformat date", source: "SubmitDate", dest: "PostingDate", rule: "date:yyyy-MM-dd|dd/MM/yyyy", result: "Changes the date format" },
  { pattern: "Array element", source: "Lines[0].Amount", dest: "FirstLineAmount", rule: "—", result: "One array item by index" }
];

const notify = useNotify();
const { confirm } = useConfirm();
const tenantStore = useTenantStore();
const { canChooseTenant, tenantOptions, loadingTenants, loadTenants, tenantName } = useTenantOptions();

// Super admins target any tenant via the dropdown; other roles use their active tenant.
const selectedTenantId = ref(null);
const tenantId = computed(() => (canChooseTenant.value && selectedTenantId.value ? selectedTenantId.value : tenantStore.activeTenantId));

const systemOptions = ["Concur", "Maconomy", "Paycor"].map((s) => ({ label: s, value: s }));

const fmt = useDateFormat();
const columns = computed(() => [
  ...(canChooseTenant.value
    ? [{ name: "tenant", label: "Tenant", field: () => tenantName(tenantId.value), align: "left", default: true, filterable: false }]
    : []),
  { name: "sourceSystem", label: "Source system", field: "sourceSystem", align: "left", sortable: true, default: true },
  { name: "destinationSystem", label: "Destination system", field: "destinationSystem", align: "left", sortable: true, default: true },
  { name: "sourceField", label: "Source field", field: "sourceField", align: "left", sortable: true, default: true },
  { name: "destinationField", label: "Destination field", field: "destinationField", align: "left", sortable: true, default: true },
  { name: "transformationRule", label: "Transformation", field: "transformationRule", align: "left", sortable: true },
  { name: "isActive", label: "Status", field: "isActive", align: "left", sortable: true, default: true },
  { name: "createdBy", label: "Created By", field: "createdBy", align: "left", sortable: true },
  { name: "updatedBy", label: "Updated By", field: "updatedBy", align: "left", sortable: true },
  { name: "createdOnUtc", label: "Created", field: (r) => fmt.formatDateTime(r.createdOnUtc), align: "left", sortable: true },
  { name: "updatedOnUtc", label: "Updated", field: (r) => fmt.formatDateTime(r.updatedOnUtc), align: "left", sortable: true, default: true },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const filters = reactive({ sourceSystem: null, destinationSystem: null });
const { rows, loading, totalRecords: total, selected, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) => {
    if (!tenantId.value) return Promise.resolve({ data: [], total: 0 });
    return mappingApi.list(tenantId.value, {
      page,
      limit,
      sourceSystem: filters.sourceSystem || undefined,
      destinationSystem: filters.destinationSystem || undefined,
      search: search.value || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords }));
  },
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Server-side filtering: reload (debounced, first page) on any search/filter/tenant change.
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters, tenantId], reload, { deep: true });

// Super admins: load tenant options and default the selector to the active tenant.
onMounted(() => {
  if (canChooseTenant.value) {
    loadTenants();
    selectedTenantId.value = tenantStore.activeTenantId;
  }
});

const filterChips = computed(() => {
  const chips = [];
  if (filters.sourceSystem) chips.push({ key: "sourceSystem", label: `Source: ${filters.sourceSystem}` });
  if (filters.destinationSystem) chips.push({ key: "destinationSystem", label: `Destination: ${filters.destinationSystem}` });
  return chips;
});
const removeFilter = (key) => { filters[key] = null; };
const clearFilters = () => { filters.sourceSystem = null; filters.destinationSystem = null; };

// Create / edit
const formOpen = ref(false);
const editing = ref(false);
const saving = ref(false);
const formRef = ref(null);
const form = reactive({ id: null, sourceSystem: "Concur", destinationSystem: "Maconomy", sourceField: "", destinationField: "", transformationRule: "", isActive: true });

const resetForm = () => {
  Object.assign(form, { id: null, sourceSystem: "Concur", destinationSystem: "Maconomy", sourceField: "", destinationField: "", transformationRule: "", isActive: true });
  editing.value = false;
};
const openCreate = () => { resetForm(); formOpen.value = true; };
const openEdit = (row) => {
  resetForm();
  editing.value = true;
  Object.assign(form, { id: row.id, sourceSystem: row.sourceSystem, destinationSystem: row.destinationSystem, sourceField: row.sourceField, destinationField: row.destinationField, transformationRule: row.transformationRule || "", isActive: row.isActive });
  formOpen.value = true;
};

const submitForm = async ({ clearDraft } = {}) => {
  if (!(await formRef.value?.validate())) return;

  if (!editing.value) {
    const dup = rows.value.find((r) =>
      r.isActive && r.sourceSystem === form.sourceSystem && r.destinationSystem === form.destinationSystem && r.sourceField === form.sourceField);
    if (dup) {
      const ok = await confirm({
        title: "Replace existing mapping",
        message: "An active rule for this field pair exists and will be replaced — continue?",
        confirmLabel: "Replace"
      });
      if (!ok) return;
    }
  }

  saving.value = true;
  try {
    if (editing.value) {
      await mappingApi.update(tenantId.value, form.id, {
        destinationField: form.destinationField,
        transformationRule: form.transformationRule || null,
        isActive: form.isActive
      });
      notify.success("Mapping updated.");
    } else {
      await mappingApi.create(tenantId.value, {
        sourceSystem: form.sourceSystem,
        destinationSystem: form.destinationSystem,
        sourceField: form.sourceField,
        destinationField: form.destinationField,
        transformationRule: form.transformationRule || null,
        isActive: true
      });
      notify.success("Mapping saved.");
    }
    clearDraft?.();
    formOpen.value = false;
    resetForm();
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

const toggleActive = async (row) => {
  if (row.isActive) {
    const ok = await confirm({
      title: "Deactivate mapping",
      message: "The default mapping will be used for this field. Continue?",
      confirmLabel: "Deactivate",
      type: "danger"
    });
    if (!ok) return;
  }
  try {
    await mappingApi.update(tenantId.value, row.id, { isActive: !row.isActive });
    notify.success("Mapping updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const remove = async (row) => {
  const ok = await confirm({
    title: "Delete mapping",
    message: `Delete the mapping for "${row.sourceField}"?`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await mappingApi.remove(tenantId.value, row.id);
    notify.success("Mapping deleted.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const bulkSetActive = async (sel, active) => {
  if (!sel.length) return;
  const ok = await confirm({
    title: active ? "Activate mappings" : "Deactivate mappings",
    message: `${active ? "Activate" : "Deactivate"} ${sel.length} mapping(s)?`,
    type: active ? "primary" : "danger"
  });
  if (!ok) return;
  try {
    await Promise.all(sel.map((r) => mappingApi.update(tenantId.value, r.id, { isActive: active })));
    notify.success("Mappings updated.");
    selected.value = [];
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const bulkRemove = async (sel) => {
  if (!sel.length) return;
  const ok = await confirm({
    title: "Delete mappings",
    message: `Delete ${sel.length} mapping(s)? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await Promise.all(sel.map((r) => mappingApi.remove(tenantId.value, r.id)));
    notify.success("Mappings deleted.");
    selected.value = [];
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
</style>
