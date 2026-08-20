export default [
  {
    path: "/roles",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "roles",
        component: () => import("modules/role/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["roles.write"], title: "Roles" }
      },
      {
        // The role in full: its definition, the groups composing it, and who holds it in this tenant.
        // Gated as the list is — a tenant admin reaches a platform role here read-only, and the page
        // decides what they may change from the `canManage` the server sends with the role.
        path: ":id",
        name: "role_detail",
        component: () => import("modules/role/pages/detail.vue"),
        meta: { requiresAuth: true, permissions: ["roles.write"], title: "Role" }
      }
    ]
  }
];
