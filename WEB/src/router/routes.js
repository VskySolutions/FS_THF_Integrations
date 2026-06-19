const routes = [
  // Home redirects to the dashboard, which renders the role-appropriate view (super / tenant / common).
  { path: "/", redirect: "/dashboard" },
  { path: "/not-authorized", name: "not_authorized", component: () => import("src/pages/not_authorized.vue"), meta: { title: "Not Authorized" } },
  { path: "/:catchAll(.*)*", component: () => import("pages/error.vue"), meta: { title: "Error" } }
];

export default routes;
