const roles = ["Operator", "TenantAdmin", "SuperAdmin"];

export default [
  {
    path: "/jobs",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "jobs",
        component: () => import("modules/integration/pages/jobs.vue"),
        meta: { requiresAuth: true, roles, title: "Integration Jobs" }
      }
    ]
  },
  {
    path: "/logs",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "logs",
        component: () => import("modules/integration/pages/logs.vue"),
        meta: { requiresAuth: true, roles: ["TenantAdmin", "SuperAdmin"], title: "Logs" }
      }
    ]
  },
  {
    path: "/health",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "health",
        component: () => import("modules/integration/pages/health.vue"),
        meta: { requiresAuth: true, roles: ["TenantAdmin", "SuperAdmin"], title: "Health" }
      }
    ]
  }
];
