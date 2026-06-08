const routes = [
  {
    path: "/auth",
    component: () => import("layouts/auth_layout.vue"),
    children: [
      { path: "", component: () => import("modules/auth/pages/login.vue"), meta: { title: "Login" } },
      { path: "login", name: "login", component: () => import("modules/auth/pages/login.vue"), meta: { title: "Login" } }
    ]
  }
];
export default routes;
