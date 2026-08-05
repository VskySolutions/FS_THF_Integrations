<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Saved Views' }]"
      show-back
      @back="$router.back()"
    />

    <app-data-table
      page-key="uf_saved_views"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      default-sort-by="updatedOnUtc"
      @refresh="load"
    >
      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ formatDateTime(cell.row.createdOnUtc) }}</q-td>
      </template>
      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense icon="o_edit" @click="rename(cell.row)" />
          <q-btn flat round dense icon="o_delete" color="negative" @click="remove(cell.row)" />
        </q-td>
      </template>
    </app-data-table>
  </q-page>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { Dialog } from "quasar";
import { ufSavedViewApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { useAuditColumns } from "composables/useAuditColumns";
import AppDataTable from "components/common/AppDataTable.vue";
import AppListHeader from "components/common/AppListHeader.vue";

const auditColumns = useAuditColumns();
const notify = useNotify();
const { confirm } = useConfirm();
const { formatDateTime } = useDateFormat();

const rows = ref([]);
const loading = ref(false);

const columns = [
  { name: "name", label: "Name", field: "name", align: "left", sortable: true, default: true },
  { name: "listPage", label: "List Page", field: "listPage", align: "left", sortable: true, default: true },
  { name: "ownerName", label: "Created By", field: "ownerName", align: "left", default: true },
  { name: "createdOnUtc", label: "Created", field: "createdOnUtc", align: "left", sortable: true, default: true },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

const load = async () => {
  loading.value = true;
  try {
    rows.value = (await ufSavedViewApi.shared()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const rename = (row) => {
  Dialog.create({
    title: "Rename view",
    prompt: { model: row.name, type: "text" },
    cancel: { flat: true, noCaps: true },
    ok: { color: "primary", unelevated: true, noCaps: true, label: "Save" }
  }).onOk(async (name) => {
    try {
      await ufSavedViewApi.update(row.id, { name: name.trim(), isShared: true });
      notify.success("View renamed.");
      await load();
    } catch (err) {
      notify.error(getApiErrorMessage(err));
    }
  });
};

const remove = async (row) => {
  const ok = await confirm({
    title: "Delete shared view",
    message: `Delete "${row.name}"? Users who set it as their default will revert to All Records.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await ufSavedViewApi.remove(row.id);
    notify.success("View deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(load);
</script>
