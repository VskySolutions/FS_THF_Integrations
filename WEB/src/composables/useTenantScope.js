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
// The fetch currently in flight, shared by every caller: the layout and the toolbar button both ask for
// the list as they mount, and one request should serve them both.
let inflight = null;
// Bumped whenever the cache is deliberately emptied, so a request already in the air cannot land
// afterwards and fill it back in.
let generation = 0;

/**
 * Forget everything this module is holding.
 *
 * The state is deliberately at MODULE scope so one selection drives every screen — which also means it
 * OUTLIVES a session: signing out is a router navigation, not a page load, so nothing here is torn down.
 * Left alone, the next person to sign in inherits the last one's scope selection and their cached tenant
 * list, which is why a tenant renamed in one session was still read by its old name in the next.
 *
 * Raised by the auth store as `session-cleared` rather than imported, to keep the store and this
 * composable from importing each other.
 */
const forget = () => {
  generation += 1;
  scopeTenantId.value = null;
  tenantOptions.value = [];
  loadingTenants.value = false;
  inflight = null;
};

// Never rejects: an unreachable list is a dropdown that stays empty, not an error for a caller to handle.
const fetchTenants = async () => {
  const mine = generation;
  loadingTenants.value = true;
  try {
    const resp = await tenantApi.list({ page: 1, limit: 100 });
    if (mine !== generation) return; // forgotten while this was in the air
    tenantOptions.value = (resp?.data || []).map((t) => ({ label: t.name, value: t.tenantId }));
  } catch {
    // non-fatal: the dropdown simply stays empty
  } finally {
    if (mine === generation) loadingTenants.value = false;
  }
};

if (typeof window !== "undefined") {
  window.addEventListener("session-cleared", forget);
}

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

  // `force` re-reads a list already held. Every caller on a page LOAD wants the cheap version — a list
  // already fetched, or the fetch already running — but a tenant that has just been renamed or created
  // makes the cached list wrong, and the name shown in the toolbar is read straight out of it.
  const loadTenants = async ({ force = false } = {}) => {
    if (!canScopeTenant.value) return;
    if (inflight) {
      await inflight;
      // A forced read cannot settle for the answer that request is giving: it went out BEFORE the change
      // it is being asked to reflect. Everyone else has now been served by it.
      if (!force) return;
    }
    if (!force && tenantOptions.value.length) return;
    const run = fetchTenants();
    inflight = run;
    try {
      await run;
    } finally {
      if (inflight === run) inflight = null;
    }
  };

  /**
   * Re-read the list after a tenant has been created, renamed, deactivated or archived.
   *
   * Called by the screens that do those things. The toolbar's label and its menu are rendered from this
   * list, so without it a rename shows everywhere in the app except the one control that names the
   * tenant you are looking at.
   */
  const refreshTenants = () => loadTenants({ force: true });

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
    refreshTenants,
    setScope,
    clearScope
  };
}
