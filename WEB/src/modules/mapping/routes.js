export default [
  {
    path: "/mappings",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "mappings",
        component: () => import("modules/mapping/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["mappings.read"], title: "Mapping Configuration" }
      }
    ]
  }
];
