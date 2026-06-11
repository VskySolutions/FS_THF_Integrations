export default [
  {
    path: "/tenants",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "tenants",
        component: () => import("modules/tenant/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["tenants.write"], title: "Tenants" }
      },
      {
        path: ":id",
        name: "tenant_detail",
        component: () => import("modules/tenant/pages/detail.vue"),
        meta: { requiresAuth: true, permissions: ["tenants.write"], title: "Tenant" }
      }
    ]
  }
];
