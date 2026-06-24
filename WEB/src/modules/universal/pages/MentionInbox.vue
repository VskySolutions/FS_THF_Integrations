<template>
  <q-page padding>
    <div class="row items-center q-mb-md">
      <div class="text-h6">My Mentions</div>
    </div>

    <div class="row q-gutter-sm q-mb-md">
      <app-select v-model="entityFilter" :options="entityOptions" label="Entity type" style="min-width: 200px;" @update:model-value="reload" />
      <app-select v-model="readFilter" :options="readOptions" label="Status" style="min-width: 160px;" @update:model-value="reload" />
    </div>

    <app-data-table
      page-key="uf_mentions"
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
      <template #body-cell-entity="cell">
        <q-td :props="cell">
          <q-icon :name="iconFor(cell.row.entityType)" color="primary" class="q-mr-xs" />
          {{ labelFor(cell.row.entityType) }}
        </q-td>
      </template>
      <template #body-cell-preview="cell">
        <q-td :props="cell" class="cursor-pointer" @click="open(cell.row)">
          <div class="text-grey-8 ellipsis-2-lines">{{ cell.row.preview }}</div>
          <div class="fs-12 text-grey-6">by {{ cell.row.authorName || "Unknown" }}</div>
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
import { ufNotificationApi, getApiErrorMessage, EntityType } from "services/api";
import { useListTable } from "composables/useListTable";
import { useNotify } from "composables/useNotify";
import { useDateFormat } from "composables/useDateFormat";
import { useEntityMeta } from "composables/uf/useEntityMeta";
import AppDataTable from "components/common/AppDataTable.vue";
import AppSelect from "components/common/AppSelect.vue";

const router = useRouter();
const notify = useNotify();
const { formatDateTime } = useDateFormat();
const { iconFor, labelFor, routeFor } = useEntityMeta();

const entityFilter = ref(null);
const readFilter = ref(null);
const entityOptions = [
  { label: "All", value: null },
  { label: "Customer Request", value: EntityType.CustomerRequest },
  { label: "Integration Job", value: EntityType.IntegrationJob },
  { label: "Tenant", value: EntityType.Tenant },
  { label: "User", value: EntityType.User },
  { label: "User Group", value: EntityType.UserGroup }
];
const readOptions = [
  { label: "All", value: null },
  { label: "Unread", value: false },
  { label: "Read", value: true }
];

const columns = [
  { name: "entity", label: "Record", field: "entityType", align: "left", default: true },
  { name: "preview", label: "Mention", field: "preview", align: "left", default: true },
  { name: "createdOnUtc", label: "Date", field: "createdOnUtc", align: "left", sortable: true, default: true },
  { name: "status", label: "Status", field: "isRead", align: "left", default: true }
];

const { rows, loading, totalRecords, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    ufNotificationApi.mentions({
      page,
      limit,
      entityType: entityFilter.value ?? undefined,
      isRead: readFilter.value === null ? undefined : readFilter.value
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const reload = () => {
  pagination.value.page = 1;
  load();
};

const open = async (m) => {
  if (!m.isRead) {
    try { await ufNotificationApi.markMentionRead(m.id); } catch { /* ignore */ }
  }
  router.push(routeFor(m.entityType, m.entityId));
};
</script>

<style scoped>
.ellipsis-2-lines { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
</style>
