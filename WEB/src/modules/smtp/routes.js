export default [
  {
    path: "/smtp-accounts",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "smtp_accounts",
        component: () => import("modules/smtp/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["email.manage"], title: "Email Accounts" }
      }
    ]
  }
];
