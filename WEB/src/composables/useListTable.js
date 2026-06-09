import { ref, onMounted, onBeforeUnmount } from "vue";

// Standard list-page plumbing for AppDataTable (WO-59): data fetching, server
// pagination state, quick-search/filter-drawer state, refresh, and automatic
// reload on tenant switch. Page-specific concerns (columns, client-side
// filtering, CRUD actions) stay in the page.
//
//   const list = useListTable({
//     fetcher: ({ page, limit }) =>
//       tenantApi.list({ page, limit }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
//     onError: (err) => notify.error(getApiErrorMessage(err))
//   });
//
// `fetcher({ page, limit })` must resolve to `{ data: Row[], total: number }`.
export function useListTable ({ fetcher, defaultPageSize = 20, onError, reloadOnTenantSwitch = true } = {}) {
  const rows = ref([]);
  const loading = ref(false);
  const totalRecords = ref(0);
  const search = ref("");
  const filterOpen = ref(false);
  const selected = ref([]);
  const pagination = ref({
    page: 1,
    rowsPerPage: defaultPageSize,
    sortBy: null,
    descending: false,
    rowsNumber: 0
  });

  const load = async () => {
    loading.value = true;
    try {
      const result = await fetcher({ page: pagination.value.page, limit: pagination.value.rowsPerPage });
      rows.value = result?.data ?? [];
      totalRecords.value = result?.total ?? rows.value.length;
    } catch (err) {
      if (onError) onError(err);
    } finally {
      loading.value = false;
    }
  };

  // AppDataTable emits "request" only for page/size changes (sorting is client-side).
  const onRequest = (pag) => {
    pagination.value = { ...pagination.value, ...pag };
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
