<template>
  <q-page padding>
    <app-breadcrumbs :items="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Mapping Configuration' }]" />

    <div class="row items-center q-mb-md q-gutter-sm">
      <div class="text-h5 text-weight-bold">Mapping Configuration</div>
      <q-space />
      <q-input v-model="search" dense outlined debounce="300" placeholder="Search fields" style="max-width: 260px;">
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>
      <q-btn unelevated no-caps color="primary" icon="o_add" label="Add Mapping" :disable="!tenantId" @click="openCreate" />
    </div>

    <q-banner v-if="!tenantId" dense class="bg-orange-1 text-orange-9 q-mb-md">
      <template #avatar><q-icon name="o_warning" color="orange" /></template>
      No active tenant selected.
    </q-banner>

    <app-filter-drawer :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-select v-model="filters.sourceSystem" :options="systemOptions" label="Source system" class="q-mb-md" />
      <app-select v-model="filters.destinationSystem" :options="systemOptions" label="Destination system" />
    </app-filter-drawer>

    <app-data-table
      page-key="mappings"
      row-key="id"
      title="Field mappings"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :total-records="total"
      :pagination="pagination"
      @request="onRequest"
      @refresh="load"
    >
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
        <app-select v-model="form.sourceSystem" :options="systemOptions" label="Source system *" class="q-mb-md" :clearable="false" :disable="editing" />
        <app-select v-model="form.destinationSystem" :options="systemOptions" label="Destination system *" class="q-mb-md" :clearable="false" :disable="editing" />
        <q-input v-model="form.sourceField" outlined stack-label hide-bottom-space label="Source field *" class="q-mb-md" :disable="editing" :rules="[(v) => !!v || 'Required']" />
        <q-input v-model="form.destinationField" outlined stack-label hide-bottom-space label="Destination field *" class="q-mb-md" :rules="[(v) => !!v || 'Required']" />
        <q-input v-model="form.transformationRule" outlined stack-label hide-bottom-space label="Transformation rule (optional)" type="textarea" autogrow />
      </q-form>
    </app-form-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onBeforeUnmount } from "vue";
import { mappingApi, getApiErrorMessage } from "services/api";
import { useTenantStore } from "stores/tenant";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const tenantStore = useTenantStore();
const tenantId = computed(() => tenantStore.activeTenantId);

const systemOptions = ["Concur", "Maconomy", "Paycor"].map((s) => ({ label: s, value: s }));

const columns = [
  { name: "sourceSystem", label: "Source system", field: "sourceSystem", align: "left", sortable: true },
  { name: "destinationSystem", label: "Destination system", field: "destinationSystem", align: "left", sortable: true },
  { name: "sourceField", label: "Source field", field: "sourceField", align: "left", sortable: true },
  { name: "destinationField", label: "Destination field", field: "destinationField", align: "left" },
  { name: "transformationRule", label: "Transformation", field: "transformationRule", align: "left" },
  { name: "isActive", label: "Status", field: "isActive", align: "left", sortable: true },
  { name: "actions", label: "", field: "actions", align: "right" }
];

const rows = ref([]);
const loading = ref(false);
const total = ref(0);
const search = ref("");
const filters = reactive({ sourceSystem: null, destinationSystem: null });
const pagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });

const filterChips = computed(() => {
  const chips = [];
  if (filters.sourceSystem) chips.push({ key: "sourceSystem", label: `Source: ${filters.sourceSystem}` });
  if (filters.destinationSystem) chips.push({ key: "destinationSystem", label: `Destination: ${filters.destinationSystem}` });
  return chips;
});
const removeFilter = (key) => { filters[key] = null; };
const clearFilters = () => { filters.sourceSystem = null; filters.destinationSystem = null; };

const filteredRows = computed(() => {
  let result = rows.value;
  if (filters.sourceSystem) result = result.filter((r) => r.sourceSystem === filters.sourceSystem);
  if (filters.destinationSystem) result = result.filter((r) => r.destinationSystem === filters.destinationSystem);
  const q = search.value.trim().toLowerCase();
  if (q) {
    result = result.filter((r) =>
      r.sourceField?.toLowerCase().includes(q) || r.destinationField?.toLowerCase().includes(q));
  }
  return result;
});

const load = async () => {
  if (!tenantId.value) return;
  loading.value = true;
  try {
    const resp = await mappingApi.list(tenantId.value, { page: pagination.value.page, limit: pagination.value.rowsPerPage });
    rows.value = resp?.data || [];
    total.value = resp?.meta?.totalRecords ?? rows.value.length;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};
const onRequest = (pag) => { pagination.value = { ...pagination.value, ...pag }; load(); };

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

const onTenantSwitched = () => load();
onMounted(() => {
  load();
  window.addEventListener("tenant-switched", onTenantSwitched);
});
onBeforeUnmount(() => window.removeEventListener("tenant-switched", onTenantSwitched));
</script>
