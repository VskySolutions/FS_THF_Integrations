import { ref, watch, onMounted, unref } from "vue";
import { dashboardApi, getApiErrorMessage } from "services/api";

// Dashboard data composables (WO-73). Each owns independent loading/error/state and re-fetches when
// the date range (or tenant) changes. Errors are captured on the composable's `error` ref and NEVER
// thrown — one category failing must not break the others on the page.
//
// Each composable accepts (dateRange: Ref<string>, tenantId?: Ref<Guid|null>).

const paramsFrom = (dateRange, tenantId) => {
  const params = { dateRange: unref(dateRange) };
  const tid = unref(tenantId);
  if (tid) params.tenantId = tid;
  return params;
};

// ---- Jobs ----
export function useJobsDashboard (dateRange, tenantId = null) {
  const loading = ref(false);
  const error = ref(null);
  const kpis = ref(null);
  const successRate = ref(0);
  const volumeChart = ref([]);
  const flowBreakdown = ref([]);
  const failedJobs = ref([]);
  const retryQueueCount = ref(0);
  const retryQueueNextRunUtc = ref(null);

  const refresh = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await dashboardApi.jobs(paramsFrom(dateRange, tenantId));
      kpis.value = data?.kpis ?? null;
      successRate.value = data?.successRate ?? 0;
      volumeChart.value = data?.volumeChart ?? [];
      flowBreakdown.value = data?.flowBreakdown ?? [];
      failedJobs.value = data?.failedJobs ?? [];
      retryQueueCount.value = data?.retryQueueCount ?? 0;
      retryQueueNextRunUtc.value = data?.retryQueueNextRunUtc ?? null;
    } catch (err) {
      error.value = getApiErrorMessage(err);
    } finally {
      loading.value = false;
    }
  };

  watch(() => [unref(dateRange), unref(tenantId)], refresh);
  onMounted(refresh);

  return {
    loading,
    error,
    refresh,
    kpis,
    successRate,
    volumeChart,
    flowBreakdown,
    failedJobs,
    retryQueueCount,
    retryQueueNextRunUtc
  };
}

// ---- Health ----
export function useHealthDashboard () {
  const loading = ref(false);
  const error = ref(null);
  const status = ref(null);
  const components = ref([]);
  const allOperational = ref(true);

  const refresh = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await dashboardApi.health();
      status.value = data?.status ?? null;
      components.value = data?.components ?? [];
      allOperational.value = data?.allOperational ?? false;
    } catch (err) {
      error.value = getApiErrorMessage(err);
    } finally {
      loading.value = false;
    }
  };

  onMounted(refresh);

  return { loading, error, refresh, status, components, allOperational };
}

// ---- Customers ----
export function useCustomerDashboard (dateRange, tenantId = null) {
  const loading = ref(false);
  const error = ref(null);
  const kpis = ref(null);
  const funnel = ref([]);
  const ageing = ref([]);
  const syncHealth = ref(null);
  const activityFeed = ref([]);
  const topSubmitters = ref([]);
  const submissionTrend = ref([]);

  const refresh = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await dashboardApi.customers(paramsFrom(dateRange, tenantId));
      kpis.value = data?.kpis ?? null;
      funnel.value = data?.funnel ?? [];
      ageing.value = data?.ageing ?? [];
      syncHealth.value = data?.syncHealth ?? null;
      activityFeed.value = data?.activityFeed ?? [];
      topSubmitters.value = data?.topSubmitters ?? [];
      submissionTrend.value = data?.submissionTrend ?? [];
    } catch (err) {
      error.value = getApiErrorMessage(err);
    } finally {
      loading.value = false;
    }
  };

  watch(() => [unref(dateRange), unref(tenantId)], refresh);
  onMounted(refresh);

  return {
    loading,
    error,
    refresh,
    kpis,
    funnel,
    ageing,
    syncHealth,
    activityFeed,
    topSubmitters,
    submissionTrend
  };
}

// ---- Users ----
export function useUserDashboard (dateRange, tenantId = null) {
  const loading = ref(false);
  const error = ref(null);
  const kpis = ref(null);
  const roleDistribution = ref([]);
  const activityFeed = ref([]);

  const refresh = async () => {
    loading.value = true;
    error.value = null;
    try {
      const data = await dashboardApi.users(paramsFrom(dateRange, tenantId));
      kpis.value = data?.kpis ?? null;
      roleDistribution.value = data?.roleDistribution ?? [];
      activityFeed.value = data?.activityFeed ?? [];
    } catch (err) {
      error.value = getApiErrorMessage(err);
    } finally {
      loading.value = false;
    }
  };

  watch(() => [unref(dateRange), unref(tenantId)], refresh);
  onMounted(refresh);

  return { loading, error, refresh, kpis, roleDistribution, activityFeed };
}

// ---- Platform (Super Admin) ----
export function usePlatformDashboard (dateRange, tenantId = null) {
  const loading = ref(false);
  const error = ref(null);
  const tenantKpis = ref(null);
  const crossTenantJobs = ref([]);
  const tenantHealth = ref([]);
  const growth = ref([]);
  const onboarding = ref([]);
  const systemAlerts = ref([]);
  const userAnalytics = ref(null);
  const customer = ref(null);

  // The platform endpoint takes no tenantId; `forceRefresh` bypasses the server cache.
  const refresh = async (forceRefresh = false) => {
    loading.value = true;
    error.value = null;
    try {
      const data = await dashboardApi.platform({ dateRange: unref(dateRange) }, forceRefresh);
      tenantKpis.value = data?.tenantKpis ?? null;
      crossTenantJobs.value = data?.crossTenantJobs ?? [];
      tenantHealth.value = data?.tenantHealth ?? [];
      growth.value = data?.growth ?? [];
      onboarding.value = data?.onboarding ?? [];
      systemAlerts.value = data?.systemAlerts ?? [];
      userAnalytics.value = data?.userAnalytics ?? null;
      customer.value = data?.customer ?? null;
    } catch (err) {
      error.value = getApiErrorMessage(err);
    } finally {
      loading.value = false;
    }
  };

  watch(() => [unref(dateRange), unref(tenantId)], () => refresh(false));
  onMounted(() => refresh(false));

  return {
    loading,
    error,
    refresh,
    tenantKpis,
    crossTenantJobs,
    tenantHealth,
    growth,
    onboarding,
    systemAlerts,
    userAnalytics,
    customer
  };
}
