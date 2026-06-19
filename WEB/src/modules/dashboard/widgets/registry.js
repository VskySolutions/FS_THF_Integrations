// Dashboard widget registry (WO-73) — the single source of truth for every dashboard widget.
//
// Each entry: { key, title, description, role, category, component }
//   role     ∈ "common" | "tenantAdmin" | "superAdmin"  (visibility tier)
//   category ∈ "jobs" | "health" | "customers" | "users" | "platform"  (data source)
//   component  lazy async import of the widget SFC
//
// The widget WOs (74/75/76) REPLACE the stub SFCs referenced here; keys/roles/categories/paths must
// stay in lockstep with the backend DashboardDefaultLayouts.

export const WIDGETS = [
  // ---- COMMON (all authenticated users) ----
  { key: "jobKpiCards", title: "Job KPIs", description: "Headline job counts and trends.", role: "common", category: "jobs", component: () => import("../common/JobKpiCards.vue") },
  { key: "jobSuccessGauge", title: "Job Success Rate", description: "Success rate gauge for the selected period.", role: "common", category: "jobs", component: () => import("../common/JobSuccessGaugeWidget.vue") },
  { key: "jobVolumeTrend", title: "Job Volume Trend", description: "Completed / failed / pending volume over time.", role: "common", category: "jobs", component: () => import("../common/JobVolumeTrendChart.vue") },
  { key: "flowBreakdown", title: "Flow Breakdown", description: "Job outcomes broken down by integration flow.", role: "common", category: "jobs", component: () => import("../common/FlowBreakdownTable.vue") },
  { key: "systemHealth", title: "System Health", description: "Live health of platform components.", role: "common", category: "health", component: () => import("../common/SystemHealthWidget.vue") },
  { key: "failedJobsPanel", title: "Failed Jobs", description: "Recent failures awaiting attention.", role: "common", category: "jobs", component: () => import("../common/FailedJobsPanel.vue") },
  { key: "retryQueue", title: "Retry Queue", description: "Jobs queued for automatic retry.", role: "common", category: "jobs", component: () => import("../common/RetryQueueWidget.vue") },

  // ---- TENANT ADMIN ----
  { key: "userSummary", title: "User Summary", description: "User counts and engagement.", role: "tenantAdmin", category: "users", component: () => import("../tenant/UserSummaryPanel.vue") },
  { key: "userRoleDistribution", title: "Role Distribution", description: "Users by role.", role: "tenantAdmin", category: "users", component: () => import("../tenant/UserRoleDistributionChart.vue") },
  { key: "customerKpiCards", title: "Customer KPIs", description: "Customer onboarding headline metrics.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerKpiCards.vue") },
  { key: "customerFunnel", title: "Customer Funnel", description: "Onboarding funnel by stage.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerFunnelWidget.vue") },
  { key: "customerAgeing", title: "Customer Ageing", description: "Requests by time-in-status with SLA flags.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerAgeingPanel.vue") },
  { key: "customerSyncHealth", title: "Customer Sync Health", description: "Sync success, failures and retries.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerSyncHealthWidget.vue") },
  { key: "customerActivityFeed", title: "Customer Activity", description: "Recent customer workflow activity.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerActivityFeedWidget.vue") },
  { key: "customerSubmissionTrend", title: "Submission Trend", description: "Submitted vs approved over time.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerSubmissionTrendWidget.vue") },

  // ---- SUPER ADMIN (platform) ----
  { key: "tenantKpiCards", title: "Tenant KPIs", description: "Platform-wide tenant and activity metrics.", role: "superAdmin", category: "platform", component: () => import("../super/TenantKpiCards.vue") },
  { key: "crossTenantJobChart", title: "Cross-Tenant Jobs", description: "Job outcomes per tenant.", role: "superAdmin", category: "platform", component: () => import("../super/CrossTenantJobChart.vue") },
  { key: "tenantHealthTable", title: "Tenant Health", description: "Per-tenant configuration and health status.", role: "superAdmin", category: "platform", component: () => import("../super/TenantHealthTable.vue") },
  { key: "platformGrowthChart", title: "Platform Growth", description: "Tenant and user growth over time.", role: "superAdmin", category: "platform", component: () => import("../super/PlatformGrowthChart.vue") },
  { key: "tenantOnboardingPanel", title: "Tenant Onboarding", description: "Tenants with incomplete setup.", role: "superAdmin", category: "platform", component: () => import("../super/TenantOnboardingPanel.vue") },
  { key: "systemAlertsPanel", title: "System Alerts", description: "Platform-wide alerts by severity.", role: "superAdmin", category: "platform", component: () => import("../super/SystemAlertsPanel.vue") },
  { key: "platformUserAnalytics", title: "User Analytics", description: "Cross-tenant user analytics.", role: "superAdmin", category: "platform", component: () => import("../super/PlatformUserAnalytics.vue") },
  { key: "crossTenantCustomerKpi", title: "Cross-Tenant Customer KPIs", description: "Customer metrics across all tenants.", role: "superAdmin", category: "platform", component: () => import("../super/CrossTenantCustomerKpi.vue") },
  { key: "customerIssuesTable", title: "Customer Issues", description: "Tenants with customer workflow issues.", role: "superAdmin", category: "platform", component: () => import("../super/CustomerIssuesTable.vue") },
  { key: "crossTenantCustomerChart", title: "Cross-Tenant Customers", description: "Customer volumes and sync per tenant.", role: "superAdmin", category: "platform", component: () => import("../super/CrossTenantCustomerChart.vue") },
  { key: "customerConversionFunnel", title: "Customer Conversion Funnel", description: "Platform-wide customer conversion funnel.", role: "superAdmin", category: "platform", component: () => import("../super/CustomerConversionFunnel.vue") }
];

// Ordered key lists used when the server returns no saved layout. Mirrors the backend defaults:
// common = 7 common keys; tenantAdmin = common + 8 tenant keys; superAdmin = tenantAdmin + 11 super keys.
const COMMON_KEYS = WIDGETS.filter((w) => w.role === "common").map((w) => w.key);
const TENANT_KEYS = WIDGETS.filter((w) => w.role === "tenantAdmin").map((w) => w.key);
const SUPER_KEYS = WIDGETS.filter((w) => w.role === "superAdmin").map((w) => w.key);

export function defaultLayoutForRole (role) {
  if (role === "superAdmin") return [...COMMON_KEYS, ...TENANT_KEYS, ...SUPER_KEYS];
  if (role === "tenantAdmin") return [...COMMON_KEYS, ...TENANT_KEYS];
  return [...COMMON_KEYS];
}

// Widgets visible to a role: common sees common; tenantAdmin sees common + tenantAdmin; superAdmin sees all.
export function widgetsForRole (role) {
  const keys = new Set(defaultLayoutForRole(role));
  return WIDGETS.filter((w) => keys.has(w.key));
}

// Quick lookup by key.
export const WIDGETS_BY_KEY = Object.freeze(
  WIDGETS.reduce((map, w) => { map[w.key] = w; return map; }, {}));
