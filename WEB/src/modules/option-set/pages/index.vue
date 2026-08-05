<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/' },
        { label: 'Tenant Settings' },
        { label: 'Option Sets' }
      ]"
      :show-add="canManage"
      add-label="New List"
      show-back
      @add="openCreate"
      @back="$router.back()"
    >
      <template #actions>
        <app-select
          v-model="entityFilter"
          :options="entityFilterOptions"
          label="Entity"
          style="min-width: 200px;"
          :clearable="false"
          @update:model-value="load"
        />
      </template>
    </app-list-header>

    <app-data-table
      page-key="option_sets"
      row-key="id"
      title="Option lists"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      default-sort-by="updatedOnUtc"
      @refresh="load"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <router-link class="text-primary text-weight-medium" :to="{ name: 'option_set_detail', params: { id: cell.row.id } }">
            {{ cell.row.name }}
          </router-link>
          <div class="fs-12 text-grey-6">{{ cell.row.key }}</div>
        </q-td>
      </template>
      <template #body-cell-entityType="cell">
        <q-td :props="cell">
          <q-icon :name="iconFor(cell.row.entityType)" color="primary" class="q-mr-xs" />
          {{ labelFor(cell.row.entityType) }}
        </q-td>
      </template>
      <template #body-cell-itemSortMode="cell">
        <q-td :props="cell">{{ sortModeLabel(cell.row.itemSortMode) }}</q-td>
      </template>
      <template #body-cell-origin="cell">
        <q-td :props="cell">
          <!-- Origin comes from isSystem, NOT isEditable: standard lists are editable now, so isEditable no
               longer distinguishes them. -->
          <q-badge :color="cell.row.isSystem ? 'grey-6' : 'primary'" :label="cell.row.isSystem ? 'Standard' : 'Custom'" />
        </q-td>
      </template>
      <template #body-cell-isActive="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.isActive ? 'positive' : 'grey-5'" :label="cell.row.isActive ? 'Active' : 'Inactive'" />
        </q-td>
      </template>
      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" :to="{ name: 'option_set_detail', params: { id: cell.row.id } }">
            <q-tooltip>View / manage values</q-tooltip>
          </q-btn>
          <q-btn v-if="cell.row.isEditable && canManage" flat round dense color="primary" icon="o_edit" @click="openEdit(cell.row)">
            <q-tooltip>Edit</q-tooltip>
          </q-btn>
          <!-- A standard list's VALUES are editable, but the list itself cannot be deleted: feature code
               references its key, and the seeder would recreate it on the next restart anyway. -->
          <q-btn v-if="!cell.row.isSystem && canManage" flat round dense color="negative" icon="o_delete" @click="remove(cell.row)">
            <q-tooltip>Delete</q-tooltip>
          </q-btn>
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width column flex-center q-pa-xl text-grey-6">
          <q-icon name="o_list_alt" size="40px" class="q-mb-sm" />
          <div class="text-subtitle1 q-mb-xs">No option lists for this entity</div>
          <div v-if="canManage" class="q-mb-md">Create a list to offer standard input values like Payment Terms.</div>
          <q-btn v-if="canManage" unelevated no-caps color="primary" icon="o_add" label="New List" @click="openCreate" />
        </div>
      </template>
    </app-data-table>

    <option-set-form-drawer v-model="formOpen" :set="editing" :sets="rows" @saved="onSaved" />
  </q-page>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { optionSetApi, getApiErrorMessage, OptionItemSortMode } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useAuditColumns } from "composables/useAuditColumns";
import { useEntityMeta } from "composables/uf/useEntityMeta";
import { useEntityTypeOptions } from "composables/useOptionSet";
import AppListHeader from "components/common/AppListHeader.vue";
import AppDataTable from "components/common/AppDataTable.vue";
import AppSelect from "components/common/AppSelect.vue";
import OptionSetFormDrawer from "modules/option-set/components/OptionSetFormDrawer.vue";

const auditColumns = useAuditColumns();
const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
const { labelFor, iconFor } = useEntityMeta();
const { options: entityTypeOptions } = useEntityTypeOptions();

const canManage = has(Permissions.OptionSetsManage);

const entityFilterOptions = [{ label: "All entities", value: null }, ...entityTypeOptions];
const entityFilter = ref(null);

const rows = ref([]);
const loading = ref(false);

const columns = [
  { name: "name", label: "Name", field: "name", align: "left", sortable: true, default: true },
  { name: "entityType", label: "Entity", field: "entityType", align: "left", sortable: true, default: true },
  { name: "itemCount", label: "Values", field: "itemCount", align: "left", sortable: true, default: true },
  { name: "itemSortMode", label: "Order", field: "itemSortMode", align: "left", default: true },
  { name: "origin", label: "Type", field: "isSystem", align: "left", default: true },
  { name: "isActive", label: "Status", field: "isActive", align: "left", default: true },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

const sortModeLabel = (mode) => ({
  [OptionItemSortMode.AlphabeticalAsc]: "Alphabetical (A → Z)",
  [OptionItemSortMode.AlphabeticalDesc]: "Alphabetical (Z → A)",
  [OptionItemSortMode.Custom]: "Custom"
}[mode] || mode);

const load = async () => {
  loading.value = true;
  try {
    rows.value = (await optionSetApi.list({ entityType: entityFilter.value ?? undefined })) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const formOpen = ref(false);
const editing = ref(null);

const openCreate = () => { editing.value = null; formOpen.value = true; };
const openEdit = (row) => { editing.value = row; formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

const remove = async (row) => {
  const ok = await confirm({
    title: "Delete option list",
    message: `Delete "${row.name}" and all its values? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await optionSetApi.remove(row.id);
    notify.success("Option list deleted.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(load);
</script>
