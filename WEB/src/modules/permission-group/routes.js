export default [
  {
    path: "/permission-groups",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "permission_groups",
        component: () => import("modules/permission-group/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["groups.manage"], title: "Permission Groups" }
      },
      {
        path: ":id",
        name: "permission_group_detail",
        component: () => import("modules/permission-group/pages/detail.vue"),
        meta: { requiresAuth: true, permissions: ["groups.manage"], title: "Permission Groups" }
      }
    ]
  }
];
