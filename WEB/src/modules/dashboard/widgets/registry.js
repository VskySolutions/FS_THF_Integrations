// Dashboard widget registry (WO-73) — the single source of truth for every dashboard widget.
//
// Each entry: { key, title, description, role, category, component }
//   role     ∈ "common" | "tenantAdmin" | "superAdmin"  (visibility tier)
//   category ∈ "customers" | "users" | "platform"  (data source)
//   component  lazy async import of the widget SFC
//
// Keys/roles/categories/paths must stay in lockstep with the backend DashboardDefaultLayouts.

export const WIDGETS = [
  // ---- TENANT ADMIN ----
  { key: "userSummary", title: "User Summary", description: "User counts and engagement.", role: "tenantAdmin", category: "users", component: () => import("../tenant/UserSummaryPanel.vue") },
  { key: "userRoleDistribution", title: "Role Distribution", description: "Users by role.", role: "tenantAdmin", category: "users", component: () => import("../tenant/UserRoleDistributionChart.vue") },
  { key: "customerKpiCards", title: "Customer KPIs", description: "Customer onboarding headline metrics.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerKpiCards.vue") },
  { key: "customerFunnel", title: "Customer Funnel", description: "Onboarding funnel by stage.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerFunnelWidget.vue") },
  { key: "customerAgeing", title: "Customer Ageing", description: "Requests by time-in-status with SLA flags.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerAgeingPanel.vue") },
  { key: "customerActivityFeed", title: "Customer Activity", description: "Recent customer workflow activity.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerActivityFeedWidget.vue") },
  { key: "customerSubmissionTrend", title: "Submission Trend", description: "Submitted vs approved over time.", role: "tenantAdmin", category: "customers", component: () => import("../tenant/CustomerSubmissionTrendWidget.vue") },

  // ---- SUPER ADMIN (platform) ----
  { key: "tenantKpiCards", title: "Tenant KPIs", description: "Platform-wide tenant and activity metrics.", role: "superAdmin", category: "platform", component: () => import("../super/TenantKpiCards.vue") },
  { key: "tenantHealthTable", title: "Tenant Health", description: "Per-tenant activity status.", role: "superAdmin", category: "platform", component: () => import("../super/TenantHealthTable.vue") },
  { key: "platformGrowthChart", title: "Platform Growth", description: "Tenant and user growth over time.", role: "superAdmin", category: "platform", component: () => import("../super/PlatformGrowthChart.vue") },
  { key: "tenantOnboardingPanel", title: "Tenant Onboarding", description: "Tenants with incomplete setup.", role: "superAdmin", category: "platform", component: () => import("../super/TenantOnboardingPanel.vue") },
  { key: "systemAlertsPanel", title: "System Alerts", description: "Platform-wide alerts by severity.", role: "superAdmin", category: "platform", component: () => import("../super/SystemAlertsPanel.vue") },
  { key: "platformUserAnalytics", title: "User Analytics", description: "Cross-tenant user analytics.", role: "superAdmin", category: "platform", component: () => import("../super/PlatformUserAnalytics.vue") },
  { key: "crossTenantCustomerKpi", title: "Cross-Tenant Customer KPIs", description: "Customer metrics across all tenants.", role: "superAdmin", category: "platform", component: () => import("../super/CrossTenantCustomerKpi.vue") },
  { key: "customerIssuesTable", title: "Customer Issues", description: "Tenants with customer workflow issues.", role: "superAdmin", category: "platform", component: () => import("../super/CustomerIssuesTable.vue") },
  { key: "crossTenantCustomerChart", title: "Cross-Tenant Customers", description: "Customer volumes per tenant.", role: "superAdmin", category: "platform", component: () => import("../super/CrossTenantCustomerChart.vue") },
  { key: "customerConversionFunnel", title: "Customer Conversion Funnel", description: "Platform-wide customer conversion funnel.", role: "superAdmin", category: "platform", component: () => import("../super/CustomerConversionFunnel.vue") }
];

// Ordered key lists used when the server returns no saved layout. Mirrors the backend defaults:
// there are no common-tier widgets; tenantAdmin = user + customer keys; superAdmin adds the platform keys.
const COMMON_KEYS = WIDGETS.filter((w) => w.role === "common").map((w) => w.key);
const TENANT_KEYS = WIDGETS.filter((w) => w.role === "tenantAdmin").map((w) => w.key);
const SUPER_KEYS = WIDGETS.filter((w) => w.role === "superAdmin").map((w) => w.key);
// Customer onboarding charts/reports — shown to customer-workflow users (data entry / review / approve).
const CUSTOMER_KEYS = WIDGETS.filter((w) => w.category === "customers" && w.role === "tenantAdmin").map((w) => w.key);

export function defaultLayoutForRole (role) {
  if (role === "superAdmin") return [...COMMON_KEYS, ...TENANT_KEYS, ...SUPER_KEYS];
  if (role === "tenantAdmin") return [...COMMON_KEYS, ...TENANT_KEYS];
  if (role === "customer") return [...COMMON_KEYS, ...CUSTOMER_KEYS];
  return [...COMMON_KEYS];
}

// Widgets visible to a role: customer sees the customer charts; tenantAdmin sees all tenant widgets;
// superAdmin sees everything.
export function widgetsForRole (role) {
  const keys = new Set(defaultLayoutForRole(role));
  return WIDGETS.filter((w) => keys.has(w.key));
}

// Customer-related charts/reports are the only widgets shown by default; the rest are hidden and can be
// switched on from Customise. Mirrors DashboardDefaultLayouts.DefaultHiddenFor on the backend.
const isCustomerWidget = (w) => w.category === "customers" || /customer/i.test(w.key);

export function defaultHiddenForRole (role) {
  const visible = widgetsForRole(role);
  // Roles without any customer widgets hide nothing — keep their dashboard full.
  if (!visible.some(isCustomerWidget)) return [];
  return visible.filter((w) => !isCustomerWidget(w)).map((w) => w.key);
}

// Quick lookup by key.
export const WIDGETS_BY_KEY = Object.freeze(
  WIDGETS.reduce((map, w) => { map[w.key] = w; return map; }, {}));
