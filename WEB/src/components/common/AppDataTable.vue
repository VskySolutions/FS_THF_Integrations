<template>
  <q-table
    v-model:pagination="innerPagination"
    v-model:selected="innerSelected"
    :rows="displayedRows"
    :columns="effectiveColumns"
    :visible-columns="effectiveVisibleColumns"
    :row-key="rowKey"
    :loading="loading"
    :selection="selectable ? 'multiple' : 'none'"
    :rows-per-page-options="rowsPerPageOptions"
    flat
    bordered
    :class="['app-data-table', { 'with-selection': selectable }]"
    @request="onRequest"
  >
    <!-- Top bar: title, custom actions, column menu, refresh -->
    <template #top>
      <div class="row full-width items-center q-gutter-sm">
        <div v-if="title" class="text-subtitle1 text-weight-medium">{{ title }}</div>
        <q-space />
        <slot name="actions" />

        <q-btn flat round dense icon="o_view_column">
          <q-tooltip>Columns</q-tooltip>
          <q-menu>
            <q-list dense style="min-width: 180px;">
              <q-item-label header class="text-grey-7">Show columns</q-item-label>
              <q-item v-for="col in toggleableColumns" :key="col.name" tag="label" clickable>
                <q-item-section side>
                  <q-checkbox v-model="visibleColumnNames" :val="col.name" dense />
                </q-item-section>
                <q-item-section>{{ col.label }}</q-item-section>
              </q-item>
              <q-separator />
              <q-item clickable @click="resetColumns">
                <q-item-section class="text-primary">Reset columns</q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </q-btn>

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

    <!-- Resizable header cells (sorting still works via q-th :props). -->
    <template #header-cell="props">
      <q-th :props="props" :style="columnStyle(props.col.name)" class="app-th">
        {{ props.col.label }}
        <div
          v-if="props.col.name !== 'actions'"
          class="app-th__resize"
          @mousedown.stop.prevent="startResize($event, props.col.name)"
          @click.stop
        />
      </q-th>
    </template>

    <!-- Forward all parent-provided body slots (e.g. body-cell-xxx) to QTable. -->
    <template v-for="(_, name) in forwardedSlots" #[name]="slotProps" :key="name">
      <slot :name="name" v-bind="slotProps" />
    </template>
  </q-table>
</template>

<script setup>
import { computed, ref, watch, useSlots, toRef } from "vue";
import { usePreferences } from "composables/usePreferences";
import useColumnResize from "composables/dataTable/useColumnResize.js";

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

const innerPagination = ref({
  sortBy: prefs?.get("sortBy", null) ?? null,
  descending: prefs?.get("descending", false) ?? false,
  page: 1,
  rowsPerPage: prefs?.get("pageSize", 20) ?? 20,
  rowsNumber: props.totalRecords,
  ...(props.pagination || {})
});

const innerSelected = ref([]);
watch(innerSelected, (val) => emit("update:selected", val));

watch(() => props.totalRecords, (total) => {
  innerPagination.value = { ...innerPagination.value, rowsNumber: total };
});

// ---- Client-side sort of the current page's rows ----
const displayedRows = computed(() => {
  const { sortBy, descending } = innerPagination.value;
  if (!sortBy) return props.rows;
  const col = props.columns.find((c) => c.name === sortBy);
  const field = col?.field ?? sortBy;
  const get = typeof field === "function" ? field : (row) => row[field];
  const sorted = [...props.rows].sort((a, b) => {
    const x = get(a);
    const y = get(b);
    if (x == null && y == null) return 0;
    if (x == null) return 1;
    if (y == null) return -1;
    if (typeof x === "number" && typeof y === "number") return x - y;
    return String(x).localeCompare(String(y), undefined, { numeric: true });
  });
  return descending ? sorted.reverse() : sorted;
});

const onRequest = (requestProps) => {
  const next = requestProps.pagination;
  const current = innerPagination.value;
  const pageChanged = next.page !== current.page || next.rowsPerPage !== current.rowsPerPage;

  innerPagination.value = { ...current, ...next };
  if (prefs) {
    prefs.merge({ pageSize: next.rowsPerPage, sortBy: next.sortBy, descending: next.descending });
  }

  // Page/size changes reload from the server; sort-only changes are handled client-side.
  if (pageChanged) {
    emit("update:pagination", innerPagination.value);
    emit("request", innerPagination.value);
  }
};

// ---- Column visibility (persisted) ----
const toggleableColumns = computed(() => props.columns.filter((c) => c.name !== "actions"));
const allColumnNames = computed(() => props.columns.map((c) => c.name));

const visibleColumnNames = ref(
  prefs?.get("visibleColumns", null) ?? props.columns.filter((c) => c.name !== "actions").map((c) => c.name)
);

// "actions" is always shown; the menu only toggles data columns.
const effectiveVisibleColumns = computed(() => {
  const set = new Set(visibleColumnNames.value);
  if (allColumnNames.value.includes("actions")) set.add("actions");
  return [...set];
});

watch(visibleColumnNames, (val) => prefs?.set("visibleColumns", val), { deep: true });

const resetColumns = () => {
  visibleColumnNames.value = props.columns.filter((c) => c.name !== "actions").map((c) => c.name);
};

// ---- Column resizing (persisted) ----
const columnsRef = toRef(props, "columns");
const resizeWidths = ref(prefs?.get("columnWidths", {}) ?? {});
const { startResize } = useColumnResize({
  columns: columnsRef,
  resizeWidths,
  saveResizableWidthState: (widths) => prefs?.set("columnWidths", widths)
});

const columnStyle = (name) => {
  const w = resizeWidths.value?.[name];
  return w ? { width: `${w}px`, minWidth: `${w}px` } : {};
};

// Apply widths to body cells too, so columns line up with the resized headers.
const effectiveColumns = computed(() =>
  props.columns.map((c) => {
    const w = resizeWidths.value?.[c.name];
    return w ? { ...c, style: `width:${w}px;min-width:${w}px;` } : c;
  }));

// Slots other than the ones defined here are forwarded straight to QTable.
const slots = useSlots();
const reserved = ["top", "no-data", "actions", "bulk-actions", "header-cell"];
const forwardedSlots = computed(() =>
  Object.fromEntries(Object.entries(slots).filter(([name]) => !reserved.includes(name))));
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
.app-th {
  position: relative;
}
.app-th__resize {
  position: absolute;
  top: 0;
  right: 0;
  width: 6px;
  height: 100%;
  cursor: col-resize;
  user-select: none;
}
.app-th__resize:hover {
  background: var(--q-primary);
  opacity: 0.4;
}

/* Left-align the selection (checkbox) column header + body cells. */
.app-data-table.with-selection :deep(thead th:first-child),
.app-data-table.with-selection :deep(tbody td:first-child) {
  text-align: left;
  width: 1%;
  white-space: nowrap;
}
</style>
