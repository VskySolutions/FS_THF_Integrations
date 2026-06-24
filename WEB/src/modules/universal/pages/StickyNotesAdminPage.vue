<template>
  <q-page padding>
    <div class="row items-center q-mb-md">
      <div class="text-h6">Tenant Sticky Notes</div>
      <q-space />
      <q-btn unelevated no-caps color="primary" icon="o_add" label="New team note" @click="openCreate" />
    </div>

    <app-data-table
      page-key="uf_sticky_admin"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      default-sort-by="createdOnUtc"
      @refresh="load"
    >
      <template #body-cell-expiresAtUtc="cell">
        <q-td :props="cell">{{ cell.row.expiresAtUtc ? formatDateTime(cell.row.expiresAtUtc) : "—" }}</q-td>
      </template>
      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ formatDateTime(cell.row.createdOnUtc) }}</q-td>
      </template>
      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense icon="o_delete" color="negative" @click="remove(cell.row)" />
        </q-td>
      </template>
    </app-data-table>

    <q-dialog v-model="createOpen">
      <q-card style="min-width: 340px;">
        <q-card-section class="text-h6">New team sticky note</q-card-section>
        <q-card-section class="q-pt-none column q-gutter-sm">
          <app-text-field v-model="form.title" label="Title" />
          <app-text-field v-model="form.body" label="Note *" type="textarea" autogrow />
          <q-input v-model="form.expiresAt" type="datetime-local" outlined dense label="Expires (optional)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Cancel" />
          <q-btn unelevated no-caps color="primary" label="Create" :disable="!form.body.trim()" :loading="creating" @click="create" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
import { ref, reactive, onMounted } from "vue";
import { ufStickyNoteApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import AppDataTable from "components/common/AppDataTable.vue";
import AppTextField from "components/common/AppTextField.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const { formatDateTime } = useDateFormat();

const rows = ref([]);
const loading = ref(false);

const columns = [
  { name: "title", label: "Title", field: "title", align: "left", default: true },
  { name: "scope", label: "Scope", field: "scope", align: "left", default: true },
  { name: "createdOnUtc", label: "Created", field: "createdOnUtc", align: "left", sortable: true, default: true },
  { name: "expiresAtUtc", label: "Expires", field: "expiresAtUtc", align: "left", default: true },
  { name: "dismissalCount", label: "Dismissals", field: "dismissalCount", align: "left", sortable: true, default: true },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

const createOpen = ref(false);
const creating = ref(false);
const form = reactive({ title: "", body: "", expiresAt: "" });

const load = async () => {
  loading.value = true;
  try {
    rows.value = (await ufStickyNoteApi.adminList()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const openCreate = () => {
  form.title = "";
  form.body = "";
  form.expiresAt = "";
  createOpen.value = true;
};

const create = async () => {
  creating.value = true;
  try {
    await ufStickyNoteApi.create({
      title: form.title || null,
      body: form.body.trim(),
      colour: "#fff9c4",
      scope: "global",
      isPersonal: false,
      expiresAtUtc: form.expiresAt ? new Date(form.expiresAt).toISOString() : null
    });
    createOpen.value = false;
    notify.success("Team note created.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    creating.value = false;
  }
};

const remove = async (row) => {
  const ok = await confirm({ title: "Delete note", message: "Delete this team sticky note for everyone?", confirmLabel: "Delete", type: "danger" });
  if (!ok) return;
  try {
    await ufStickyNoteApi.remove(row.id);
    notify.success("Note deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(load);
</script>
