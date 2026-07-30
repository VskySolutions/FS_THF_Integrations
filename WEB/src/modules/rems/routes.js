// REMS (Phase 15, WO-115). Partner Dashboard + Admin Pool lists and the shared request detail.
// Each route is permission-gated (the router guard reads meta.permissions → hasAnyPermission):
// the Partner areas require rems.requests.read, the Admin Pool requires rems.pool.read. The EMS
// Inbox / Client Forms / Approvals nav items belong to later WOs and resolve to their own routes.
export default [
  {
    path: "/rems",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "partner",
        name: "rems_partner",
        component: () => import("modules/rems/pages/PartnerDashboard.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.read"], title: "REMS Partner Dashboard" }
      },
      {
        path: "admin-pool",
        name: "rems_admin_pool",
        component: () => import("modules/rems/pages/AdminPool.vue"),
        meta: { requiresAuth: true, permissions: ["rems.pool.read"], title: "REMS Admin Pool" }
      },
      {
        path: "requests/:id",
        name: "rems_request_detail",
        component: () => import("modules/rems/pages/RequestDetail.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.read"], title: "REMS Request" }
      }
    ]
  }
];
