import { computed, ref } from "vue";
import { LocalStorage } from "quasar";
import { tenantApi } from "services/api";
import { useAuthStore } from "stores/auth";
import { useTenantStore } from "stores/tenant";

// Super-Admin tenant scope. Unlike the tenant SWITCHER (which is limited to the tenants a user is actually
// assigned to and swaps their token), this re-points the ambient tenant on the server via the X-Tenant-Id
// header — so a Super Admin can administer a tenant they hold no assignment in.
//
// The selection is deliberately GLOBAL: one choice drives every tenant-scoped screen, so Option Sets and
// REMS can never be looking at different tenants at the same time. State lives at module scope (one shared
// instance) and in LocalStorage, which is where the axios interceptor reads it from.
export const TENANT_SCOPE_KEY = "adminTenantOverride";

const scopeTenantId = ref(LocalStorage.getItem(TENANT_SCOPE_KEY) || null);
const tenantOptions = ref([]);
const loadingTenants = ref(false);

export function useTenantScope () {
  const authStore = useAuthStore();
  const tenantStore = useTenantStore();

  // Mirrors the backend guard exactly — the header is only honoured for a Super Admin, so nobody else is
  // shown a control that would silently do nothing.
  const canScopeTenant = computed(() => authStore.roles.includes("SuperAdmin"));

  // Falls back to the caller's own tenant so the dropdown always shows where they actually are.
  const selectedTenantId = computed(() => scopeTenantId.value || tenantStore.activeTenantId);

  // True while viewing somebody else's tenant — worth saying out loud, since every screen is affected.
  const isScoped = computed(() =>
    !!scopeTenantId.value && scopeTenantId.value !== tenantStore.activeTenantId);

  const scopedTenantName = computed(() =>
    tenantOptions.value.find((t) => t.value === selectedTenantId.value)?.label || "");

  const loadTenants = async () => {
    if (!canScopeTenant.value || tenantOptions.value.length) return;
    loadingTenants.value = true;
    try {
      const resp = await tenantApi.list({ page: 1, limit: 100 });
      tenantOptions.value = (resp?.data || []).map((t) => ({ label: t.name, value: t.tenantId }));
    } catch {
      // non-fatal: the dropdown simply stays empty
    } finally {
      loadingTenants.value = false;
    }
  };

  const setScope = (tenantId) => {
    const next = tenantId || null;
    if (next === scopeTenantId.value) return;
    scopeTenantId.value = next;
    if (next) LocalStorage.set(TENANT_SCOPE_KEY, next);
    else LocalStorage.remove(TENANT_SCOPE_KEY);
    // Every list page already reloads on this event (see useListTable), so one dispatch refreshes the app
    // instead of each page wiring up its own watcher.
    window.dispatchEvent(new Event("tenant-switched"));
  };

  const clearScope = () => setScope(null);

  return {
    canScopeTenant,
    selectedTenantId,
    isScoped,
    scopedTenantName,
    tenantOptions,
    loadingTenants,
    loadTenants,
    setScope,
    clearScope
  };
}
