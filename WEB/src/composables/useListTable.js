import { ref, onMounted, onBeforeUnmount } from "vue";
import { usePreferences } from "composables/usePreferences";

// Standard list-page plumbing for AppDataTable (WO-59): data fetching, server
// pagination AND SORTING state, quick-search/filter-drawer state, refresh, and
// automatic reload on tenant switch. Page-specific concerns (columns, client-side
// filtering, CRUD actions) stay in the page.
//
//   const list = useListTable({
//     pageKey: "tenants",
//     fetcher: ({ page, limit, sortBy, descending }) =>
//       tenantApi.list({ page, limit, sortBy, descending })
//         .then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
//     onError: (err) => notify.error(getApiErrorMessage(err))
//   });
//
// `fetcher({ page, limit, sortBy, descending })` must resolve to `{ data: Row[], total: number }`.
//
// SORTING IS THE SERVER'S. A list is one page of a larger set, so ordering it in the browser only ever
// ordered the twenty rows already on screen — "newest first" meant "newest of this page", and page 2 held
// rows that belonged on page 1. It also compared what the cells DISPLAY, so a column of dates rendered
// MM/DD/YYYY sorted by month before year. Both go away by asking the server for the order it should
// return rows in: `sortBy` names a column, the endpoint maps it to a real ORDER BY over the whole set,
// and an unknown name falls back to that list's default rather than erroring.
export function useListTable ({
  fetcher,
  defaultPageSize = 20,
  // The platform convention — every list opens on what was touched most recently. A list whose rows have
  // no such column (an event feed, say) names its own.
  defaultSortBy = "updatedOnUtc",
  defaultDescending = true,
  // Same key the page gives AppDataTable. The reader's chosen sort is remembered per page, and it is kept
  // HERE rather than in the table because the fetch has to go out already carrying it — a preference the
  // table restored after the first request would have loaded the wrong page of rows.
  pageKey = "",
  onError,
  reloadOnTenantSwitch = true
} = {}) {
  const prefs = pageKey ? usePreferences(pageKey) : null;

  const rows = ref([]);
  const loading = ref(false);
  const totalRecords = ref(0);
  const search = ref("");
  const filterOpen = ref(false);
  const selected = ref([]);

  const savedSortBy = prefs?.get("sortBy", undefined);
  const savedDescending = prefs?.get("descending", undefined);

  const pagination = ref({
    page: 1,
    rowsPerPage: prefs?.get("pageSize", defaultPageSize) ?? defaultPageSize,
    sortBy: savedSortBy === undefined ? defaultSortBy : savedSortBy,
    descending: savedDescending === undefined ? defaultDescending : savedDescending,
    rowsNumber: 0
  });

  const load = async () => {
    loading.value = true;
    try {
      const { page, rowsPerPage, sortBy, descending } = pagination.value;
      const result = await fetcher({ page, limit: rowsPerPage, sortBy, descending });
      rows.value = result?.data ?? [];
      totalRecords.value = result?.total ?? rows.value.length;
    } catch (err) {
      if (onError) onError(err);
    } finally {
      loading.value = false;
    }
  };

  // AppDataTable emits "request" for a page, a page-size or a SORT change — every one of which is a
  // different question to ask the server.
  const onRequest = (pag) => {
    const previous = pagination.value;
    const next = { ...previous, ...pag };
    // A new sort is a new list, not a new page of the old one: the row that was 40th may now be 1st, and
    // staying on page 3 would show the reader the middle of an order they have not seen the start of.
    if (next.sortBy !== previous.sortBy || next.descending !== previous.descending) {
      next.page = 1;
    }
    pagination.value = next;
    prefs?.merge({ pageSize: next.rowsPerPage, sortBy: next.sortBy, descending: next.descending });
    load();
  };

  const onTenantSwitched = () => load();

  onMounted(() => {
    load();
    if (reloadOnTenantSwitch) {
      window.addEventListener("tenant-switched", onTenantSwitched);
    }
  });

  onBeforeUnmount(() => {
    if (reloadOnTenantSwitch) {
      window.removeEventListener("tenant-switched", onTenantSwitched);
    }
  });

  return { rows, loading, totalRecords, search, filterOpen, selected, pagination, load, onRequest };
}
