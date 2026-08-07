export default [
  {
    path: "/option-sets",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "",
        name: "option_sets",
        component: () => import("modules/option-set/pages/index.vue"),
        meta: { requiresAuth: true, permissions: ["optionSets.manage"], title: "Option Sets" }
      },
      {
        path: ":id",
        name: "option_set_detail",
        component: () => import("modules/option-set/pages/detail.vue"),
        meta: { requiresAuth: true, permissions: ["optionSets.manage"], title: "Option Set" }
      }
    ]
  }
];
