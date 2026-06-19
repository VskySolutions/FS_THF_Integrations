export default [
  {
    path: "/dashboard",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "dashboard",
        component: () => import("modules/dashboard/pages/DashboardPage.vue"),
        // Visible to every authenticated user; the page itself tailors widgets to the user's role.
        meta: { requiresAuth: true, title: "Dashboard" }
      }
    ]
  }
];
