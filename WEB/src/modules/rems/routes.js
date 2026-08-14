// REMS (Phase 16, initiator-first rebuild). THREE lists and one form.
//
// The Partner/CSE creates a request, fills the whole thing — client details AND engagement setup — and
// sends the intake link to the client themselves. The Admin appears only once the client has answered,
// as a reviewer who can return the setup for rework. So the Admin Pool, the EMS Inbox and the Build EMS
// screen are all gone: nothing waits in a pool to be picked up, and CSE + Industry Group are fields on
// the initiator's own form rather than a separate admin step.
//
// Each route is permission-gated (the router guard reads meta.permissions → hasAnyPermission).
export default [
  {
    path: "/rems",
    component: () => import("layouts/layout.vue"),
    children: [
      {
        path: "partner",
        name: "rems_partner",
        component: () => import("modules/rems/pages/PartnerDashboard.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.read"], title: "My REMS Requests" }
      },
      {
        // The Admin's only surface: requests whose clients have answered, opened for review of BOTH the
        // client's intake and the engagement setup. Grew out of Client Forms; /rems/client-forms redirects
        // here so old links and bookmarks still land.
        path: "ems-review",
        name: "rems_ems_review",
        component: () => import("modules/rems/pages/EmsReview.vue"),
        meta: { requiresAuth: true, permissions: ["rems.engagements.manage"], title: "EMS Review" }
      },
      { path: "client-forms", redirect: { name: "rems_ems_review" } },
      // THE form (WO-118): one tabbed page covering client information and the complete engagement setup.
      // Replaces the old create/edit drawer, the entity tab strip and the four-step
      // Setup/Marketing/Commission/Approval wizard. Reached from both lists — what a given user may EDIT
      // depends on the request's stage, not on which list they came from.
      //
      // Three paths, one component. Creating, editing and reading are the same page, and which of the
      // three you are on is the URL itself rather than a flag hung off it: /requests/new,
      // /requests/edit/:id, /requests/:id. The page reads its own route name; nothing carries a `mode`.
      {
        // A request that does not exist yet has no id to live under and nothing to read — it is the form
        // and only the form. Gated on CREATE: reading requests is not permission to raise one.
        path: "requests/new",
        name: "rems_request_new",
        component: () => import("modules/rems/pages/RemsRequestForm.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.create"], title: "New REMS Request" }
      },
      {
        path: "requests/edit/:id",
        name: "rems_request_edit",
        component: () => import("modules/rems/pages/RemsRequestForm.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.read"], title: "REMS Request" }
      },
      {
        // The read-only request detail. The old separate detail page is folded into this, which shows
        // everything it did and the whole engagement besides — so this is the plain permalink for a
        // request, and what REMS notifications and emailed links point at.
        path: "requests/:id",
        name: "rems_request",
        component: () => import("modules/rems/pages/RemsRequestForm.vue"),
        meta: { requiresAuth: true, permissions: ["rems.requests.read"], title: "REMS Request" }
      },
      // Links minted before the split: /rems/requests/:id/form, with ?mode=edit deciding which of the
      // two it meant, and /rems/engagements/:id from the workspace this page replaced. `mode` is dropped
      // on the way through — it is what the new paths say instead.
      {
        path: "requests/:id/form",
        redirect: (to) => {
          const { mode, ...query } = to.query;
          if (to.params.id === "new") return { name: "rems_request_new", query };
          const name = mode === "edit" ? "rems_request_edit" : "rems_request";
          return { name, params: { id: to.params.id }, query };
        }
      },
      { path: "engagements/:id", redirect: (to) => ({ name: "rems_request", params: { id: to.params.id } }) },
      {
        // The task-isolated Approval Inbox (WO-117 Part B): the approver's OWN pending + historical tasks.
        // Deliberately NOT permission-gated — anyone can be made an approver (the CSE, a commission
        // recipient, or someone added on the Approval tab), so no permission can predict who needs this
        // page, and gating it locked real approvers out. Task-isolation is enforced server-side: the list
        // returns only the caller's own tasks, so a user with none simply sees an empty inbox.
        path: "approvals",
        name: "rems_approvals",
        component: () => import("modules/rems/pages/ApprovalInbox.vue"),
        meta: { requiresAuth: true, title: "REMS Approvals" }
      },
      {
        // The approval-task review packet, the caller's checklist, and the checklist-gated Approve /
        // reason-required Reject decision. Ungated for the same reason as the inbox; the server 404s any
        // task that is not the caller's own, which is the real boundary.
        path: "approvals/:taskId",
        name: "rems_approval_task",
        component: () => import("modules/rems/pages/ApprovalTaskDetail.vue"),
        meta: { requiresAuth: true, title: "Approval Task" }
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
