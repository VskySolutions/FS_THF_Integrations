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
      }
    ]
  }
];
