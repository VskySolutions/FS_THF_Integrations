import { defineStore } from "pinia";
import { LocalStorage } from "quasar";

// TenantStore scaffold (WO-49). State + basic mutations only;
// data-loading and tenant-switch logic are wired in WO-50.
export const useTenantStore = defineStore("tenant", {
  state: () => ({
    tenants: LocalStorage.getItem("tenants") || [],
    activeTenantId: LocalStorage.getItem("activeTenantId") || null,
    loading: false
  }),

  getters: {
    activeTenant: (state) => state.tenants.find((t) => t.id === state.activeTenantId) || null,
    hasMultipleTenants: (state) => state.tenants.length > 1
  },

  actions: {
    setTenants (list) {
      this.tenants = Array.isArray(list) ? list : [];
      LocalStorage.set("tenants", this.tenants);
    },

    setActiveTenant (tenantId) {
      this.activeTenantId = tenantId;
      LocalStorage.set("activeTenantId", tenantId);
    },

    clear () {
      this.tenants = [];
      this.activeTenantId = null;
      this.loading = false;
      LocalStorage.remove("tenants");
      LocalStorage.remove("activeTenantId");
    }
  }
});
