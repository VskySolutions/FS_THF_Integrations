export default [
  {
    path: "/email-templates",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "email_templates",
        component: () => import("modules/email-template/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["email.manage"], title: "Email Templates" }
      }
    ]
  }
];
