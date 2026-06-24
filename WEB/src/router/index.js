import { route } from "quasar/wrappers";
import { LocalStorage } from "quasar";
import { createRouter, createMemoryHistory, createWebHistory, createWebHashHistory } from "vue-router";
import routes from "./routes";
import { useAuthStore } from "stores/auth";
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
import tenantRoutes from "modules/tenant/routes";
import personRoutes from "modules/person/routes";
import userRoutes from "modules/user/routes";
import roleRoutes from "modules/role/routes";
import integrationRoutes from "modules/integration/routes";
import mappingRoutes from "modules/mapping/routes";
import customerRoutes from "modules/customer/routes";
import permissionGroupRoutes from "modules/permission-group/routes";
import userGroupRoutes from "modules/user-group/routes";
import dashboardRoutes from "modules/dashboard/routes";
import smtpRoutes from "modules/smtp/routes";
import emailTemplateRoutes from "modules/email-template/routes";

routes.push(...accountRoutes);
routes.push(...authRoutes);
routes.push(...tenantRoutes);
routes.push(...personRoutes);
routes.push(...userRoutes);
routes.push(...roleRoutes);
routes.push(...integrationRoutes);
routes.push(...mappingRoutes);
routes.push(...customerRoutes);
routes.push(...permissionGroupRoutes);
routes.push(...userGroupRoutes);
routes.push(...dashboardRoutes);
routes.push(...smtpRoutes);
routes.push(...emailTemplateRoutes);

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

    // Already logged in but on an auth page → send to the dashboard (role-appropriate view).
    if (token && to.path.startsWith("/auth")) {
      return next("/dashboard");
    }

    if (token) {
      const authStore = useAuthStore(store);

      // mustChangePassword gate: force the change-password page first.
      if (authStore.mustChangePassword && to.name !== "change_password") {
        return next({ name: "change_password" });
      }

      // Permission gate: route meta `permissions` requires any one of the listed permission keys
      // (decoded from the active-tenant JWT). The deepest matched record's list wins.
      const requiredPermissions = to.matched.reduce((permissions, record) => {
        return Array.isArray(record.meta.permissions) ? record.meta.permissions : permissions;
      }, null);

      if (Array.isArray(requiredPermissions) && requiredPermissions.length) {
        if (!authStore.hasAnyPermission(requiredPermissions)) {
          useNotify().notifyWarning("You do not have permission to access that page.");
          return next("/");
        }
      }
    }

    next();
  });
  return Router;
});
