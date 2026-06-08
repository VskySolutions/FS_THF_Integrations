import { defineStore } from "pinia";
import { LocalStorage } from "quasar";
import { authApi } from "services/api";

// TenantStore (WO-50): active tenant context, the user's tenant assignments,
// tenant switching (with an unsaved-form guard), and the scoped-token swap.
export const useTenantStore = defineStore("tenant", {
  state: () => ({
    assignments: LocalStorage.getItem("tenantAssignments") || [],
    activeTenantId: LocalStorage.getItem("activeTenantId") || null,
    hasUnsavedForm: false,
    loading: false
  }),

  getters: {
    activeTenant: (state) =>
      state.assignments.find((t) => t.tenantId === state.activeTenantId) || state.assignments[0] || null,
    activeRole: (state) => {
      const t = state.assignments.find((x) => x.tenantId === state.activeTenantId);
      return t?.role || null;
    },
    hasMultipleTenants: (state) => state.assignments.length > 1
  },

  actions: {
    setAssignments (list) {
      this.assignments = Array.isArray(list) ? list : [];
      LocalStorage.set("tenantAssignments", this.assignments);
      if (!this.activeTenantId && this.assignments.length) {
        this.setActiveTenant(this.assignments[0].tenantId);
      }
    },

    setActiveTenant (tenantId) {
      this.activeTenantId = tenantId;
      LocalStorage.set("activeTenantId", tenantId);
    },

    setUnsavedForm (value) {
      this.hasUnsavedForm = !!value;
    },

    async loadProfile () {
      const { useAuthStore } = await import("stores/auth");
      return useAuthStore().loadProfile();
    },

    // AC-UI-007.2 / AC-UI-007.4. `confirm` is an optional async callback used to
    // resolve the unsaved-form guard (wired to useConfirm() in the views).
    async switchTenant (tenantId, { confirm } = {}) {
      if (tenantId === this.activeTenantId) {
        return true;
      }
      if (this.hasUnsavedForm && typeof confirm === "function") {
        const proceed = await confirm();
        if (!proceed) {
          return false;
        }
      }

      this.loading = true;
      try {
        const resp = await authApi.switchTenant(tenantId);
        const data = resp?.data;
        if (data?.accessToken) {
          const { useAuthStore } = await import("stores/auth");
          useAuthStore()._setTokens(data.accessToken, null);
        }
        this.setActiveTenant(tenantId);
        this.hasUnsavedForm = false;
        return true;
      } finally {
        this.loading = false;
      }
    },

    clear () {
      this.assignments = [];
      this.activeTenantId = null;
      this.hasUnsavedForm = false;
      this.loading = false;
      LocalStorage.remove("tenantAssignments");
      LocalStorage.remove("activeTenantId");
    }
  }
});
