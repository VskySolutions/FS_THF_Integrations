export default [
  {
    path: "/persons",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "persons",
        component: () => import("modules/person/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["persons.read"], title: "Person" }
      },
      {
        path: ":id",
        name: "person_detail",
        component: () => import("modules/person/pages/detail.vue"),
        meta: { requiresAuth: true, permissions: ["persons.read"], title: "Person" }
      }
    ]
  }
];
