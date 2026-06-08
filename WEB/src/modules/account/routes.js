export default [
  {
    path: "/account",
    component: () => import("layouts/layout.vue"),
    children: [
      { path: "", name: "account", component: () => import("modules/account/pages/index.vue"), meta: { requiresAuth: true, title: "Account" } },
      { path: "profile", name: "profile", component: () => import("modules/account/pages/profile.vue"), meta: { requiresAuth: true, title: "My Profile" } },
      { path: "change-password", name: "change_password", component: () => import("modules/account/pages/change_password.vue"), meta: { requiresAuth: true, title: "Change Password" } }
    ]
  }
];
