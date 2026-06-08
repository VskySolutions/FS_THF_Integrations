// src/composables/useSiteTableState.js

import { ref, unref } from "vue";
import { getLocalStorage, setLocalStorage } from "assets/utils";

export default function useSiteTableState ({
  storageKey,
  siteId,
  tableKey = "dataTable",

  defaultSearch = {},
  defaultPagination = {},
  defaultSorts = {},
  defaultResizableWidth = {},
  defaultColumns = []
}) {
  const createDefaultTableState = () => ({
    search: {},
    pagination: {},
    activeRowId: null,
    sorts: {},
    resizableWidth: {},
    columnsHideUnHide: [],
    expandedRows: [],
    rowPagination: {}
  });

  const getStorage = () => {
    try {
      const data = getLocalStorage(storageKey);

      if (!data || typeof data !== "object") {
        return {};
      }

      return data;
    } catch (error) {
      console.error("Failed to parse localStorage:", error);
      return {};
    }
  };

  const getCurrentSiteId = () => {
    return unref(siteId) || "default";
  };

  const getSiteState = () => {
    const storage = getStorage();
    const currentSiteId = getCurrentSiteId();

    if (
      !storage[currentSiteId] ||
      typeof storage[currentSiteId] !== "object"
    ) {
      storage[currentSiteId] = {};
      setLocalStorage(storageKey, storage);
    }

    return storage[currentSiteId];
  };

  const getTableState = () => {
    const siteState = getSiteState();

    return (
      siteState[tableKey] ||
      createDefaultTableState()
    );
  };

  const saveTableState = (updater) => {
    const storage = getStorage();
    const currentSiteId = getCurrentSiteId();

    if (
      !storage[currentSiteId] ||
      typeof storage[currentSiteId] !== "object"
    ) {
      storage[currentSiteId] = {};
    }

    const existingSite = storage[currentSiteId];

    const existingTable =
      existingSite[tableKey] ||
      createDefaultTableState();

    storage[currentSiteId][tableKey] =
      updater(existingTable);

    setLocalStorage(storageKey, storage);
  };

  const saveState = (payload = {}) => {
    saveTableState(existing => ({
      ...existing,
      ...payload
    }));
  };

  const saveTableSection = (section, data) => {
    saveTableState(existingTable => ({
      ...existingTable,

      [section]: data
    }));
  };

  const currentTableState = getTableState();

  const search = ref({
    ...defaultSearch,
    ...(currentTableState?.search || {})
  });

  const pagination = ref({
    ...defaultPagination,
    ...(currentTableState?.pagination || {})
  });

  const activeRowId = ref(
    currentTableState?.activeRowId || null
  );

  const sorts = ref({
    ...defaultSorts,
    ...(currentTableState?.sorts || {})
  });

  const resizeWidths = ref({
    ...defaultResizableWidth,
    ...(currentTableState?.resizableWidth || {})
  });

  const selectedColumnNames = ref(
    currentTableState?.columnsHideUnHide?.length
      ? [...currentTableState.columnsHideUnHide]
      : [...defaultColumns]
  );

  const saveDataTableState = (payload = {}) => {
    saveTableState(existingTable => ({
      ...existingTable,

      search:
        payload.search ?? existingTable.search ?? search.value,

      pagination:
        payload.pagination ?? existingTable.pagination ?? pagination.value,

      activeRowId:
        payload.activeRowId ?? existingTable.activeRowId ?? activeRowId.value,

      sorts:
        payload.sorts ?? existingTable.sorts ?? sorts.value,

      resizableWidth:
        payload.resizableWidth ??
        existingTable.resizableWidth ??
        resizeWidths.value,

      columnsHideUnHide:
        payload.columnsHideUnHide ??
        existingTable.columnsHideUnHide ??
        selectedColumnNames.value,

      ...payload
    }));
  };

  const saveResizableWidthState = (
    widths = resizeWidths.value
  ) => {
    saveTableSection(
      "resizableWidth",
      widths
    );
  };

  const saveColumnsState = (
    columns = selectedColumnNames.value
  ) => {
    saveTableSection(
      "columnsHideUnHide",
      columns
    );
  };

  const clearTableState = () => {
    const storage = getStorage();
    const currentSiteId = getCurrentSiteId();

    if (
      storage?.[currentSiteId]?.[tableKey]
    ) {
      delete storage[currentSiteId][tableKey];

      setLocalStorage(storageKey, storage);
    }
  };

  const clearSiteState = () => {
    const storage = getStorage();
    const currentSiteId = getCurrentSiteId();

    if (storage?.[currentSiteId]) {
      delete storage[currentSiteId];

      setLocalStorage(storageKey, storage);
    }
  };

  return {
    search,
    pagination,
    activeRowId,
    sorts,

    resizeWidths,
    selectedColumnNames,

    getStorage,
    getSiteState,
    getTableState,

    saveState,
    saveDataTableState,
    saveResizableWidthState,
    saveColumnsState,

    clearTableState,
    clearSiteState
  };
}
