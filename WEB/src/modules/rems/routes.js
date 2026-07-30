// REMS (Phase 15, WO-115/116). Partner Dashboard + Admin Pool lists, the shared request detail, and the
// Admin EMS-form workspaces (Build EMS, EMS Inbox, Client Forms). Each route is permission-gated (the
// router guard reads meta.permissions → hasAnyPermission): the Partner areas require rems.requests.read,
// the Admin Pool requires rems.pool.read, Build EMS + EMS Inbox require rems.forms.manage, and Client
// Forms requires rems.engagements.manage. The Approvals nav item + the Engagement Workspace belong to
// later WOs and resolve to their own routes.
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
        path: "ems-inbox",
        name: "rems_ems_inbox",
        component: () => import("modules/rems/pages/EmsInbox.vue"),
        meta: { requiresAuth: true, permissions: ["rems.forms.manage"], title: "EMS Inbox" }
      },
      {
        path: "client-forms",
        name: "rems_client_forms",
        component: () => import("modules/rems/pages/ClientForms.vue"),
        meta: { requiresAuth: true, permissions: ["rems.engagements.manage"], title: "Client Forms" }
      },
      {
        path: "requests/:id",
        name: "rems_request_detail",
        component: () => import("modules/rems/pages/RequestDetail.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.read"], title: "REMS Request" }
      },
      {
        path: "requests/:id/build-ems",
        name: "rems_build_ems",
        component: () => import("modules/rems/pages/BuildEmsPage.vue"),
        meta: { requiresAuth: true, permissions: ["rems.forms.manage"], title: "Build EMS Form" }
      },
      {
        // The staff REMS engagement workspace (WO-117): entity setup, marketing, commission and
        // send-for-approval for a submitted request. Reached from Client Forms / EMS Inbox. The approver
        // decision/checklist UI is a SEPARATE surface (the Approvals inbox) and does not live here.
        path: "engagements/:remsId",
        name: "rems_engagement",
        component: () => import("modules/rems/pages/EngagementWorkspace.vue"),
        meta: { requiresAuth: true, permissions: ["rems.engagements.manage"], title: "Engagement Workspace" }
      },
      {
        // The task-isolated Approval Inbox (WO-117 Part B): the approver's OWN pending + historical tasks.
        // Task-isolation is enforced server-side (the list returns only the caller's tasks; the detail 404s
        // anyone else's), so this surface never exposes another approver's tasks or an approver picker.
        path: "approvals",
        name: "rems_approvals",
        component: () => import("modules/rems/pages/ApprovalInbox.vue"),
        meta: { requiresAuth: true, permissions: ["rems.approvals.act"], title: "REMS Approvals" }
      },
      {
        // The role-scoped approval-task detail: the review data the approver's role is entitled to, the
        // per-role checklist, and the checklist-gated Approve / reason-required Reject decision.
        path: "approvals/:taskId",
        name: "rems_approval_task",
        component: () => import("modules/rems/pages/ApprovalTaskDetail.vue"),
        meta: { requiresAuth: true, permissions: ["rems.approvals.act"], title: "Approval Task" }
      }
    ]
  },
  // PUBLIC client EMS form (WO-113/116) — the anonymous, no-login onboarding form reached from the
  // emailed invite link ({App:BaseUrl}/rems/form/{inviteCode}). It lives OUTSIDE the authenticated app
  // shell (its own bare public_layout — no menu / nav / tenant switcher) and is deliberately NOT
  // permission-gated: meta.requiresAuth is false and no `permissions` are set, so the router guard lets it
  // through without a token (it matches neither the requiresAuth-redirect nor the permission gate). The
  // form itself is authorised solely by the unguessable invite code, via the unauthenticated remsPublicApi.
  {
    path: "/rems/form/:inviteCode",
    component: () => import("layouts/public_layout.vue"),
    children: [
      {
        path: "",
        name: "rems_public_form",
        component: () => import("modules/rems/pages/PublicEmsForm.vue"),
        meta: { requiresAuth: false, title: "EMS Form" }
      }
    ]
  }
];
