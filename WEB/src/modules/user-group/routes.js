export default [
  {
    path: "/user-groups",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "user_groups",
        component: () => import("modules/user-group/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["users.groupManagement"], title: "User Groups" }
      }
    ]
  }
];
