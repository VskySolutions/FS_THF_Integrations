<template>
  <q-table
    v-model:pagination="innerPagination"
    v-model:selected="innerSelected"
    :rows="rows"
    :columns="columns"
    :row-key="rowKey"
    :loading="loading"
    :selection="selectable ? 'multiple' : 'none'"
    :rows-per-page-options="rowsPerPageOptions"
    flat
    bordered
    class="app-data-table"
    @request="onRequest"
  >
    <!-- Top bar: title, bulk-action slot, custom actions, refresh -->
    <template #top>
      <div class="row full-width items-center q-gutter-sm">
        <div v-if="title" class="text-subtitle1 text-weight-medium">{{ title }}</div>
        <q-space />
        <slot name="actions" />
        <q-btn flat round dense icon="o_refresh" :loading="loading" @click="$emit('refresh')">
          <q-tooltip>Refresh</q-tooltip>
        </q-btn>
      </div>
      <div v-if="selectable && innerSelected.length" class="row full-width items-center q-mt-sm">
        <q-chip dense color="primary" text-color="white">{{ innerSelected.length }} selected</q-chip>
        <slot name="bulk-actions" :selected="innerSelected" />
      </div>
    </template>

    <template #no-data>
      <div class="full-width column flex-center q-pa-lg text-grey-6">
        <q-icon name="o_inbox" size="32px" class="q-mb-sm" />
        No Data Available
      </div>
    </template>

    <!-- Forward all parent-provided slots (e.g. body-cell-xxx) to QTable. -->
    <template v-for="(_, name) in forwardedSlots" #[name]="slotProps" :key="name">
      <slot :name="name" v-bind="slotProps" />
    </template>
  </q-table>
</template>

<script setup>
import { computed, ref, watch, useSlots } from "vue";
import { usePreferences } from "composables/usePreferences";

const props = defineProps({
  rows: { type: Array, default: () => [] },
  columns: { type: Array, default: () => [] },
  rowKey: { type: String, default: "id" },
  loading: { type: Boolean, default: false },
  title: { type: String, default: "" },
  pageKey: { type: String, default: "" },
  totalRecords: { type: Number, default: 0 },
  selectable: { type: Boolean, default: false },
  pagination: { type: Object, default: null }
});

const emit = defineEmits(["request", "refresh", "update:pagination", "update:selected"]);

const rowsPerPageOptions = [10, 20, 50, 100];
const prefs = props.pageKey ? usePreferences(props.pageKey) : null;

const defaultPagination = {
  sortBy: prefs?.get("sortBy", null) ?? null,
  descending: prefs?.get("descending", false) ?? false,
  page: 1,
  rowsPerPage: prefs?.get("pageSize", 20) ?? 20,
  rowsNumber: props.totalRecords
};

const innerPagination = ref({ ...defaultPagination, ...(props.pagination || {}) });
const innerSelected = ref([]);

// Keep rowsNumber in sync for server-side pagination.
watch(() => props.totalRecords, (total) => {
  innerPagination.value = { ...innerPagination.value, rowsNumber: total };
});

watch(innerSelected, (val) => emit("update:selected", val));

// Slots other than the ones we define here are forwarded straight to QTable.
const slots = useSlots();
const reserved = ["top", "no-data", "actions", "bulk-actions"];
const forwardedSlots = computed(() =>
  Object.fromEntries(Object.entries(slots).filter(([name]) => !reserved.includes(name))));

const onRequest = (requestProps) => {
  const { page, rowsPerPage, sortBy, descending } = requestProps.pagination;
  innerPagination.value = { ...innerPagination.value, page, rowsPerPage, sortBy, descending };
  if (prefs) {
    prefs.merge({ pageSize: rowsPerPage, sortBy, descending });
  }
  emit("update:pagination", innerPagination.value);
  emit("request", requestProps.pagination);
};
</script>

<style scoped>
.app-data-table {
  border-radius: 12px;
}
.app-data-table :deep(thead tr th) {
  position: sticky;
  top: 0;
  z-index: 1;
  background: #fff;
}
</style>
