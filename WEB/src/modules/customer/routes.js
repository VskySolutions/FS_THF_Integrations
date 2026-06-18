export default [
  {
    path: "/customers",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "customers",
        component: () => import("modules/customer/pages/index.vue"),
        // No permissions array: any authenticated user may view/create customers.
        meta: { requiresAuth: true, title: "Customers" }
      },
      {
        path: ":id",
        name: "customer_detail",
        component: () => import("modules/customer/pages/detail.vue"),
        meta: { requiresAuth: true, title: "Customers" }
      }
    ]
  }
];
