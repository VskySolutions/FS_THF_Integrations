<template>
  <q-page padding>
    <div class="row items-center q-mb-md">
      <div class="text-h6">Notifications</div>
      <q-space />
      <q-btn unelevated no-caps color="primary" icon="o_done_all" label="Mark all read" @click="markAllRead" />
    </div>

    <div class="row q-gutter-sm q-mb-md">
      <app-select v-model="typeFilter" :options="typeOptions" label="Type" style="min-width: 180px;" @update:model-value="reload" />
      <app-select v-model="readFilter" :options="readOptions" label="Status" style="min-width: 160px;" @update:model-value="reload" />
    </div>

    <app-data-table
      page-key="uf_notifications"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      default-sort-by="createdOnUtc"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-type="cell">
        <q-td :props="cell">
          <q-icon :name="metaFor(cell.row.type).icon" :color="metaFor(cell.row.type).color" size="20px" class="q-mr-xs" />
          {{ metaFor(cell.row.type).label }}
        </q-td>
      </template>
      <template #body-cell-notification="cell">
        <q-td :props="cell" class="cursor-pointer" @click="open(cell.row)">
          <div class="text-weight-medium">{{ cell.row.title }}</div>
          <div class="text-grey-7 fs-12 ellipsis-2-lines">{{ cell.row.body }}</div>
        </q-td>
      </template>
      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ formatDateTime(cell.row.createdOnUtc) }}</q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.isRead ? 'grey-5' : 'primary'" :label="cell.row.isRead ? 'Read' : 'Unread'" />
        </q-td>
      </template>
    </app-data-table>
  </q-page>
</template>

<script setup>
import { ref } from "vue";
import { useRouter } from "vue-router";
import { ufNotificationApi, getApiErrorMessage } from "services/api";
import { useListTable } from "composables/useListTable";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useNotificationMeta } from "composables/uf/useNotificationMeta";
import { useEntityMeta } from "composables/uf/useEntityMeta";
import AppDataTable from "components/common/AppDataTable.vue";
import AppSelect from "components/common/AppSelect.vue";

const router = useRouter();
const notify = useNotify();
const { formatDateTime } = useDateFormat();
const { metaFor, allTypes } = useNotificationMeta();
const { routeFor } = useEntityMeta();

const typeFilter = ref(null);
const readFilter = ref(null);
const typeOptions = [{ label: "All types", value: null }, ...allTypes.map((t) => ({ label: t.label, value: t.value }))];
const readOptions = [
  { label: "All", value: null },
  { label: "Unread", value: false },
  { label: "Read", value: true }
];

const columns = [
  { name: "type", label: "Type", field: "type", align: "left", default: true },
  { name: "notification", label: "Notification", field: "title", align: "left", default: true },
  { name: "createdOnUtc", label: "Date", field: "createdOnUtc", align: "left", sortable: true, default: true },
  { name: "status", label: "Status", field: "isRead", align: "left", default: true }
];

const { rows, loading, totalRecords, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    ufNotificationApi.list({
      page,
      limit,
      type: typeFilter.value ?? undefined,
      isRead: readFilter.value === null ? undefined : readFilter.value
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const reload = () => {
  pagination.value.page = 1;
  load();
};

const open = async (n) => {
  if (!n.isRead) {
    try { await ufNotificationApi.markRead(n.id); } catch { /* ignore */ }
  }
  if (n.entityType && n.entityId) {
    router.push(routeFor(n.entityType, n.entityId));
  } else {
    load();
  }
};

const markAllRead = async () => {
  try {
    await ufNotificationApi.markAllRead();
    notify.success("All notifications marked read.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};
</script>

<style scoped>
.ellipsis-2-lines { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
</style>
