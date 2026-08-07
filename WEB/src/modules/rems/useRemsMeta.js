import { ref, computed } from "vue";
import { useAuthStore } from "stores/auth";
import { optionSetApi, EntityType } from "services/api";
import {
  useRemsOptionCatalog, ensureRemsOptionsLoaded, REMS_OPTION_SEED
} from "modules/rems/useRemsOptionCatalog";

// Type / Priority / Status / Industry Group / Department / Service Line are TENANT-CONFIGURABLE option
// sets, so their labels come from useRemsOptionCatalog — a tenant that renames a status in Administration
// → Option Sets sees that everywhere, not just in the picker they edited it from. The arrays below are
// the catalogue's seed, re-exported for the few callers that need the closed set itself (the marking
// rules, and the sort order a filter dropdown is built in).
//
// Everything further down that looks similar — form state, approver role, approval status, engagement
// status, email events — mirrors a C# ENUM the backend branches on. Those have no option set and must not
// gain one; their maps stay here as code.
export const REMS_TYPE_OPTIONS = REMS_OPTION_SEED.type;
export const REMS_PRIORITY_OPTIONS = REMS_OPTION_SEED.priority;
export const REMS_STATUS_OPTIONS = REMS_OPTION_SEED.status;

// The two type codes the intake form picks on the partner's behalf: picking a client out of the lookup
// means THF already has them, typing a name nobody matched means they are new. Named rather than inlined
// because they are CODES — the tenant may relabel either one, and the auto-selection has to keep working.
export const REMS_TYPE_BRAND_NEW_CLIENT = "brand_new_client";
export const REMS_TYPE_EXISTING_CLIENT = "existing_client";

// Type codes that mean "an existing client is referenced" (drives the client-lookup type marking).
// A subsidiary is one too, so picking a client never overrides a partner who already chose it.
export const REMS_EXISTING_CLIENT_TYPES = [
  REMS_TYPE_EXISTING_CLIENT, "subsidiary_child_of_existing_client"
];

export const REMS_INDUSTRY_GROUP_OPTIONS = REMS_OPTION_SEED.industryGroup;

// EMS form-state codes (RemsFormStatus) used to filter the EMS Inbox by form state.
export const REMS_FORM_STATE_OPTIONS = [
  { label: "Draft", value: "Draft" },
  { label: "Saved", value: "Saved" },
  { label: "Sent", value: "Sent" },
  { label: "Submitted", value: "Submitted" },
  { label: "Cancelled", value: "Cancelled" }
];

// Whether the client has returned their form — the Client Forms list's "Form" column. Sent as a string
// and parsed to a bool server-side, because a column filter's value is always a string.
export const REMS_FORM_SUBMITTED_OPTIONS = [
  { label: "Submitted", value: "true" },
  { label: "Not submitted", value: "false" }
];

// Approval-task filters (RemsApproverRole / RemsApprovalTaskStatus names, matched server-side).
export const REMS_APPROVER_ROLE_OPTIONS = [
  { label: "CSE", value: "CSE" },
  { label: "Department Director", value: "DepartmentDirector" },
  { label: "Managing Shareholder", value: "ManagingShareholder" },
  { label: "Commission Recipient", value: "CommissionRecipient" },
  { label: "Approver", value: "Approver" }
];

export const REMS_APPROVAL_STATUS_OPTIONS = [
  { label: "Pending", value: "Pending" },
  { label: "Approved", value: "Approved" },
  { label: "Rejected", value: "Rejected" }
];

const PRIORITY_COLORS = { urgent: "red-8", high: "deep-orange-7", medium: "amber-8", low: "blue-grey-5" };
// Awaiting Customer borrows the EMS "Sent" teal — it is the same moment seen from the request — and the
// approval stages borrow ENGAGEMENT_STATUS_META's colours, so a request badge and the engagement badge
// underneath it never disagree about what pending/approved looks like.
const STATUS_COLORS = {
  draft: "grey-6",
  submitted: "primary",
  awaiting_customer: "teal-7",
  customer_submitted: "deep-purple-6",
  pending_approval: "orange-8",
  changes_requested: "negative",
  approved: "positive"
};
const EMS_STATE_LABELS = {
  NotStarted: "Not started", Draft: "Draft", Saved: "Saved", Sent: "Sent", Submitted: "Submitted", Cancelled: "Cancelled"
};
// Colour the EMS form-state chips consistently with the request-status palette.
const EMS_STATE_COLORS = {
  NotStarted: "grey-5", Draft: "grey-6", Saved: "primary", Sent: "teal-7", Submitted: "positive", Cancelled: "negative"
};
const SUBMISSION_STATE_LABELS = { Submitted: "Submitted", AwaitingCustomer: "Awaiting customer" };

// Approval-task metadata (WO-117 Part B). Approver roles mirror the backend RemsApproverRole enum; the
// task/round status strings mirror RemsApprovalTaskStatus / RemsApprovalRoundStatus.
const APPROVER_ROLE_LABELS = {
  CSE: "CSE",
  DepartmentDirector: "Department Director",
  ManagingShareholder: "Managing Shareholder",
  CommissionRecipient: "Commission Recipient",
  // A hand-picked approver with no other standing on the engagement (RemsApproverRole.Approver).
  Approver: "Approver"
};
const APPROVER_ROLE_ICONS = {
  CSE: "o_support_agent",
  DepartmentDirector: "o_account_tree",
  ManagingShareholder: "o_workspace_premium",
  CommissionRecipient: "o_payments"
};
const APPROVAL_STATUS_LABELS = { Pending: "Pending", Approved: "Approved", Rejected: "Rejected" };
const APPROVAL_STATUS_COLORS = { Pending: "orange-8", Approved: "positive", Rejected: "negative" };

// Engagement lifecycle status (REMSEngagement.Status) — label + badge colour in one lookup, shared by
// every surface that shows it (workspace tab strip, entity panel, approval panel).
const ENGAGEMENT_STATUS_META = {
  Draft: { label: "Draft", color: "grey-6" },
  PendingApproval: { label: "Pending Approval", color: "orange-8" },
  Rejected: { label: "Rejected", color: "negative" },
  Approved: { label: "Approved", color: "positive" }
};

// Provider email-delivery events (RemsFormEmailEventType). These are the ONLY events rendered — the UI
// never synthesises delivery/open state; it shows exactly what the server's email log returns.
const EMAIL_EVENT_LABELS = { Sent: "Sent", Delivered: "Delivered", Opened: "Opened", Failed: "Failed" };
const EMAIL_EVENT_COLORS = { Sent: "teal-7", Delivered: "positive", Opened: "primary", Failed: "negative" };
const EMAIL_EVENT_ICONS = { Sent: "o_send", Delivered: "o_mark_email_read", Opened: "o_drafts", Failed: "o_error" };

const labelFrom = (options, value) => options.find((o) => o.value === value)?.label || value || "—";

// "Submitted" is the status of every request sitting in the Admin Pool, which on its own says nothing
// about the one thing anyone wants to know at that stage: has somebody taken it? The backend already draws
// that line — a request is pickable while it is Submitted with no assigned admin — so the badge spells it
// out. Every other status reads exactly as it does elsewhere.
const awaitingPickUp = (row) => row?.status === "submitted" && !row?.assignedAdmin;

// Label/colour helpers for rendering REMS rows and detail cards. The option-set-backed labels read the
// shared catalogue, so a tenant's rename shows up on every badge and cell rather than only in the picker
// it was edited from; the enum-backed ones are fixed maps because the backend branches on those values.
export function useRemsMeta () {
  const options = useRemsOptionCatalog();
  const auth = useAuthStore();

  const typeLabel = (v) => labelFrom(options.type, v);
  const priorityLabel = (v) => labelFrom(options.priority, v);
  const statusLabel = (v) => labelFrom(options.status, v);
  const industryGroupLabel = (v) => labelFrom(options.industryGroup, v);
  const departmentLabel = (v) => labelFrom(options.department, v);
  const serviceLineLabel = (v) => labelFrom(options.serviceLine, v);

  // Colours stay in code: they key off the CODE, which is closed and validated server-side, so a rename
  // never strands a badge on grey. Only the wording is the tenant's to change.
  const priorityColor = (v) => PRIORITY_COLORS[v] || "grey-6";
  const statusColor = (v) => STATUS_COLORS[v] || "grey-6";
  const emsStateLabel = (v) => EMS_STATE_LABELS[v] || v || "—";
  const emsStateColor = (v) => EMS_STATE_COLORS[v] || "grey-6";
  const submissionStateLabel = (v) => (v ? (SUBMISSION_STATE_LABELS[v] || v) : "—");
  const emailEventLabel = (v) => EMAIL_EVENT_LABELS[v] || v || "—";
  const emailEventColor = (v) => EMAIL_EVENT_COLORS[v] || "grey-6";
  const emailEventIcon = (v) => EMAIL_EVENT_ICONS[v] || "o_mail";
  const approverRoleLabel = (v) => APPROVER_ROLE_LABELS[v] || v || "—";
  const approverRoleIcon = (v) => APPROVER_ROLE_ICONS[v] || "o_person";
  const approvalStatusLabel = (v) => APPROVAL_STATUS_LABELS[v] || v || "—";
  const approvalStatusColor = (v) => APPROVAL_STATUS_COLORS[v] || "grey-6";
  const engagementStatusMeta = (v) => ENGAGEMENT_STATUS_META[v] || { label: v || "—", color: "grey-6" };

  // Status badge for a request ROW (or detail) rather than a bare code: same as statusLabel/statusColor
  // except that `submitted` splits on whether an admin has picked it up. Every surface showing a request
  // — the Admin Pool, the Partner Dashboard, the request detail — uses these, so all three say the same
  // thing about the same request. Surfaces whose rows carry no assignment (the EMS Inbox, the Build EMS
  // screen) stay on the plain code helpers; they have no way to tell the two apart.
  const requestStatusLabel = (row) => {
    if (row?.status !== "submitted") return statusLabel(row?.status);
    return awaitingPickUp(row) ? "Waiting For Pickup" : "Picked Up";
  };
  const requestStatusColor = (row) => (awaitingPickUp(row) ? "amber-8" : statusColor(row?.status));

  // The EMS engagement/detail action becomes available only once the customer has submitted their
  // form (AC-REMS-002.5 / 005.6); until then it stays disabled.
  const emsDetailAvailable = (row) => row?.clientSubmissionState === "Submitted";

  // Why engagement setup is closed to this user on this row, or null when it is theirs to work.
  // Setup belongs to whoever picked the request up, so an unclaimed request has no owner and someone
  // else's is not yours to take over. The server enforces the same rule — the workspace is a URL — but
  // saying WHY on the button beats letting the click end in a 403.
  const engagementOwnerDenial = (row) => {
    const assignee = row?.assignedAdmin?.id;
    if (!assignee) return "Pick this request up first — engagement setup belongs to the assigned Admin";
    if (assignee !== auth.user?.userId) {
      return `Picked up by ${row.assignedAdmin?.name || "another Admin"} — only they can work its engagement setup`;
    }
    return null;
  };
  // The email-log / EMS-inbox action is meaningful once a form has been sent to the customer.
  const emsFormActivity = (row) =>
    !!row?.clientSubmissionState || ["Sent", "Submitted"].includes(row?.emsFormState);

  // Live option lists for pickers and column filters. Reactive, so a screen built before the catalogue
  // resolved picks up the tenant's own wording without reloading. The status FILTER carries the pool's
  // extra wording: it still filters on the `submitted` code, so its label names both of the states that
  // one code shows up as in a row rather than just the waiting one.
  const typeOptions = computed(() => options.type);
  const priorityOptions = computed(() => options.priority);
  const statusOptions = computed(() => options.status);
  const industryGroupOptions = computed(() => options.industryGroup);
  const departmentOptions = computed(() => options.department);
  const serviceLineOptions = computed(() => options.serviceLine);
  const statusFilterOptions = computed(() => options.status.map((option) =>
    (option.value === "submitted" ? { ...option, label: "Submitted/Waiting For Pickup" } : option)));

  return {
    typeLabel,
    priorityLabel,
    statusLabel,
    departmentLabel,
    serviceLineLabel,
    priorityColor,
    statusColor,
    emsStateLabel,
    emsStateColor,
    submissionStateLabel,
    industryGroupLabel,
    typeOptions,
    priorityOptions,
    statusOptions,
    industryGroupOptions,
    departmentOptions,
    serviceLineOptions,
    statusFilterOptions,
    emailEventLabel,
    emailEventColor,
    emailEventIcon,
    approverRoleLabel,
    approverRoleIcon,
    approvalStatusLabel,
    approvalStatusColor,
    engagementStatusMeta,
    requestStatusLabel,
    requestStatusColor,
    engagementOwnerDenial,
    emsDetailAvailable,
    emsFormActivity
  };
}

// The Type / Priority pickers. Delegates to the shared catalogue rather than resolving its own copy, so
// a screen that shows both a picker and a badge cannot end up with two different labels for one code.
// `load` is kept so existing callers need no change; it just awaits the catalogue.
export function useRemsOptionSets () {
  const catalog = useRemsOptionCatalog();
  return {
    typeOptions: computed(() => catalog.type),
    priorityOptions: computed(() => catalog.priority),
    load: ensureRemsOptionsLoaded
  };
}

// ---- Engagement workspace (WO-117) option sets + conditional logic ----

// Department + Service Line are stored as string codes, so — like Type/Priority/IndustryGroup — the closed
// seed lists are a safe fallback when the resolve endpoint 403s (the REMS Admin role lacks optionSets.read).
export const REMS_DEPARTMENT_CODES = Object.freeze({ CAS: "cas", TAX: "tax", AUDIT: "audit", GCS: "gcs" });
export const REMS_SERVICE_LINE_GOVERNMENT = "government";

export const REMS_DEPARTMENT_OPTIONS = REMS_OPTION_SEED.department;
export const REMS_SERVICE_LINE_OPTIONS = REMS_OPTION_SEED.serviceLine;

// The REMS.Marketing groups (from each item's MetadataJson `group` tag), in display order.
const MARKETING_GROUPS = [
  { key: "Global", label: "Global" },
  { key: "Geography", label: "Geography" },
  { key: "Service/Education", label: "Service / Education" },
  { key: "Event", label: "Event" }
];

// Conditional engagement-detail predicates — mirror the backend RemsEngagementCodes helper exactly.
export const isAuditDepartment = (department) => department === REMS_DEPARTMENT_CODES.AUDIT;
export const isTaxDepartment = (department) => department === REMS_DEPARTMENT_CODES.TAX;
export const isGovernmentAudit = (department, serviceLine) =>
  isAuditDepartment(department) && serviceLine === REMS_SERVICE_LINE_GOVERNMENT;

// Loads the engagement Department / Service Line / Marketing / Tax-Form option sets for the workspace.
// Department + Service Line degrade to the closed code lists above. Marketing + Tax Form are keyed by
// OptionSetItem *id* (the REMSEngagementMarketingMethod / REMSEngagementTaxForm FKs), so there is no closed
// fallback for their ids — the pickers are simply empty (flagged `*Unavailable`) when resolve is denied.
export function useRemsEngagementOptionSets () {
  // Department + Service Line come from the shared catalogue (same lists the labels read); Marketing and
  // Tax Form are resolved here because they are keyed by OptionSetItem *id* rather than by code, which is
  // a different shape and only this workspace needs it.
  const catalog = useRemsOptionCatalog();
  const departmentOptions = computed(() => catalog.department);
  const serviceLineOptions = computed(() => catalog.serviceLine);
  const marketingGroups = ref([]);
  const marketingUnavailable = ref(false);
  const taxFormOptions = ref([]);
  const taxFormUnavailable = ref(false);

  const groupOf = (metadataJson) => {
    try {
      return JSON.parse(metadataJson || "{}").group || "Other";
    } catch {
      return "Other";
    }
  };

  const resolveMarketing = async () => {
    try {
      const items = await optionSetApi.resolve({
        entityType: EntityType.Rems,
        key: "REMSMarketing_MarketingMethods.MarketingMethodId"
      });
      const byGroup = new Map();
      (items || []).forEach((i) => {
        const g = groupOf(i.metadataJson);
        if (!byGroup.has(g)) byGroup.set(g, []);
        byGroup.get(g).push({ value: i.id, label: i.label });
      });
      const known = MARKETING_GROUPS
        .map((g) => ({ key: g.key, label: g.label, items: byGroup.get(g.key) || [] }))
        .filter((g) => g.items.length);
      const extra = [...byGroup.keys()]
        .filter((k) => !MARKETING_GROUPS.some((g) => g.key === k))
        .map((k) => ({ key: k, label: k, items: byGroup.get(k) }));
      marketingGroups.value = [...known, ...extra];
      marketingUnavailable.value = marketingGroups.value.length === 0;
    } catch {
      marketingGroups.value = [];
      marketingUnavailable.value = true;
    }
  };

  const resolveTaxForms = async () => {
    try {
      const items = await optionSetApi.resolve({ entityType: EntityType.Rems, key: "REMS.TaxForm" });
      taxFormOptions.value = (items || []).map((i) => ({ value: i.id, label: i.label }));
      taxFormUnavailable.value = taxFormOptions.value.length === 0;
    } catch {
      taxFormOptions.value = [];
      taxFormUnavailable.value = true;
    }
  };

  const load = () => Promise.all([
    ensureRemsOptionsLoaded(),
    resolveMarketing(),
    resolveTaxForms()
  ]);

  return {
    departmentOptions,
    serviceLineOptions,
    marketingGroups,
    marketingUnavailable,
    taxFormOptions,
    taxFormUnavailable,
    load
  };
}

// The Build-EMS industry-group picker (AC-REMS-007.3), from the shared catalogue.
export function useRemsIndustryGroups () {
  const catalog = useRemsOptionCatalog();
  return {
    industryGroupOptions: computed(() => catalog.industryGroup),
    load: ensureRemsOptionsLoaded
  };
}
