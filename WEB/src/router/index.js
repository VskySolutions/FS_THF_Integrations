import { route } from "quasar/wrappers";
import { LocalStorage } from "quasar";
import { createRouter, createMemoryHistory, createWebHistory, createWebHashHistory } from "vue-router";
import routes from "./routes";
import { useAuthStore } from "stores/auth";
import { useTenantStore } from "stores/tenant";
import { useNotify } from "composables/useNotify";

/*
 * If not building with SSR mode, you can
 * directly export the Router instantiation;
 *
 * The function below can be async too; either use
 * async/await or return a Promise which resolves
 * with the Router instance.
 */

import authRoutes from "modules/auth/routes";
import accountRoutes from "modules/account/routes";

routes.push(...accountRoutes);
routes.push(...authRoutes);

export default route(function ({ store }) {
  const createHistory = process.env.SERVER
    ? createMemoryHistory
    : (process.env.VUE_ROUTER_MODE === "history" ? createWebHistory : createWebHashHistory);

  const Router = createRouter({
    scrollBehavior: () => ({ left: 0, top: 0 }),
    routes,
    history: createHistory(process.env.VUE_ROUTER_BASE)
  });

  Router.beforeEach((to, from, next) => {
    const token = LocalStorage.getItem("token");
    const requiresAuth = to.matched.some((record) => record.meta.requiresAuth);

    // Not logged in and trying to open a protected route → send to login.
    if (requiresAuth && !token) {
      return next("/auth/login");
    }

    // Already logged in but on an auth page → send to the landing page.
    if (token && to.path.startsWith("/auth")) {
      return next("/");
    }

    if (token) {
      const authStore = useAuthStore(store);

      // mustChangePassword gate: force the change-password page first.
      if (authStore.mustChangePassword && to.name !== "change_password") {
        return next({ name: "change_password" });
      }

      // Role gate: route meta `roles` restricts access to the active tenant role.
      const requiredRoles = to.matched.reduce((roles, record) => {
        return Array.isArray(record.meta.roles) ? record.meta.roles : roles;
      }, null);

      if (Array.isArray(requiredRoles) && requiredRoles.length) {
        const role = useTenantStore(store).activeRole;
        if (!role || !requiredRoles.includes(role)) {
          useNotify().notifyWarning("You do not have permission to access that page.");
          return next("/");
        }
      }
    }

    next();
  });
  return Router;
});
