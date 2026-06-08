export default [
  {
    path: "/users",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "users",
        component: () => import("modules/user/pages/index.vue"),
        meta: { requiresAuth: true, roles: ["TenantAdmin", "SuperAdmin"], title: "Users" }
      },
      {
        path: ":id",
        name: "user_detail",
        component: () => import("modules/user/pages/detail.vue"),
        meta: { requiresAuth: true, roles: ["TenantAdmin", "SuperAdmin"], title: "User" }
      }
    ]
  }
];
