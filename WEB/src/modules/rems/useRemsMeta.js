import { ref, computed } from "vue";
import { useAuthStore } from "stores/auth";
import { optionSetApi, EntityType } from "services/api";
import {
  useRemsOptionCatalog, ensureRemsOptionsLoaded, REMS_OPTION_SEED
} from "modules/rems/useRemsOptionCatalog";
import { REMS_STATUS } from "modules/rems/remsStatus";

// Type / Status / Entity Type / Department / Service Line are TENANT-CONFIGURABLE option
// sets, so their labels come from useRemsOptionCatalog — a tenant that renames a status in Administration
// → Option Sets sees that everywhere, not just in the picker they edited it from. The arrays below are
// the catalogue's seed, re-exported for the few callers that need the closed set itself (the marking
// rules, and the sort order a filter dropdown is built in).
//
// THREE OF THE ENGAGEMENT CLASSIFICATIONS READ ONE WAY ON SCREEN AND ANOTHER IN CODE. Their data —
// columns, option-set keys, API fields — keeps the older name deliberately, because each tenant's own copy
// of a list is filed under that key and so are the codes already stored against it:
//
//   industryGroup*   is labelled  "Entity Type"   — what kind of entity the client is
//   subIndustry*     is labelled  "Industry"      — the client's trade
//   subServiceLine*  is labelled  "Service Line"  — what the firm is engaged to do
//
// The helpers below keep the DATA name, so what each one reads is never in doubt; only the strings a user
// sees carry the display wording.
//
// Everything further down that looks similar — form state, approver role, approval status, engagement
// status, email events — mirrors a C# ENUM the backend branches on. Those have no option set and must not
// gain one; their maps stay here as code.
export const REMS_TYPE_OPTIONS = REMS_OPTION_SEED.type;
export const REMS_STATUS_OPTIONS = REMS_OPTION_SEED.status;

// The two type codes the intake form picks on the partner's behalf: picking a client out of the lookup
// means THF already has them, typing a name nobody matched means they are new. Named rather than inlined
// because they are CODES — the tenant may relabel either one, and the auto-selection has to keep working.
export const REMS_TYPE_BRAND_NEW_CLIENT = "brand_new_client";
export const REMS_TYPE_EXISTING_CLIENT = "existing_client";

// The three seats an engagement names, as ROLE names — the value each people-picker scopes itself by
// (remsApi.admins(role)). They mirror EmsPortal.Shared.Security.Roles exactly, spaces and all, because the
// role name IS what the API matches on and what the roles UI displays.
//
// Each was a user GROUP of the same name until the seats became roles. The names did not change, so a firm
// reads the same words; what changed is where the people are maintained — a user's own page, beside
// Partner and Admin, rather than a separate list in Administration → User Groups.
//
// Shareholder is NOT here. It is a REMS role too, but no engagement names a shareholder — holding it puts
// somebody on every engagement's approver list by default — so there is no picker to scope by it.
export const REMS_SEAT_ROLES = Object.freeze({
  CSE: "CSE",
  ENGAGEMENT_EXECUTIVE: "Engagement Executive",
  BILLING_MANAGER: "Billing Manager"
});

// Type codes that mean "an existing client is referenced" (drives the client-lookup type marking). One
// code since the subsidiary answer folded into it; kept as a list because that is what the marking rule
// reads, and because the answer has already been split and merged twice.
export const REMS_EXISTING_CLIENT_TYPES = [REMS_TYPE_EXISTING_CLIENT];

export const REMS_INDUSTRY_GROUP_OPTIONS = REMS_OPTION_SEED.industryGroup;

// The industry groups that ask the BUSINESS questions — EIN, and the Primary / Financial / Billing /
// Other contacts. The three business groups THF onboards are asked exactly the same things, so the
// split between them names what the client is without changing the form.
//
// `trust_estate` is in the family for the same reason: a trust or an estate has an EIN of its own and is
// acted for by trustees or personal representatives, so the people it names are the people who act for
// it. What it is NOT is an individual — filing one as its trustee is what put the trust's affairs under
// a person's own name.
//
// `business` is not offered in the picker but stays in this family: forms sent before the split into
// three carry the code, and a client part-way through one has to be able to finish. Mirrors the server's
// RemsFormPayloadValidator.IsBusinessGroup, which is what actually enforces it.
export const REMS_BUSINESS_INDUSTRY_GROUPS = Object.freeze([
  "not_for_profit", "insurance", "commercial", "trust_estate", "business"
]);

export const isBusinessIndustryGroup = (group) => REMS_BUSINESS_INDUSTRY_GROUPS.includes(group);

// ---- Which industries belong to which entity type ----
//
// Entity type and trade do not partition cleanly — a hospital is Health Care whether it is Commercial or
// Not-for-Profit — which is why this is a map of OVERLAPPING sets rather than a tree: Health Care and
// Educational Institutions each belong to two entity types, and are offered under both.
//
// Keyed and valued by CODE (REMS.IndustryGroup value → REMS.SubIndustry values), because a tenant may
// relabel either list and the pairing has to survive it. The ORDER shown is the option list's own, not
// this one's — a tenant who reorders their industries sees their order, filtered.
//
// Four rules make it safe on a list a tenant can edit (see remsIndustryOptions below):
//   1. NO entity type chosen yet offers NOTHING. The trade depends on what kind of entity the client is,
//      so on a new request the picker stays empty until that is answered — offering all twenty-nine and
//      then taking most of them away again is a worse way to say the same thing.
//   2. A code claimed by no entity type here — anything a tenant has ADDED — is offered under every
//      entity type. Their own configuration must not become unreachable.
//   3. An entity type with no entry here — Trust and Estate, or one a tenant adds — is not filtered at
//      all. A pairing nobody has stated is not a pairing to enforce.
//   4. Whatever is already SELECTED is always offered, even where these rules would exclude it, so
//      opening an older engagement never silently drops the industry recorded on it.
export const REMS_INDUSTRY_BY_ENTITY_TYPE = Object.freeze({
  individual: Object.freeze(["individual"]),
  government: Object.freeze(["state_government", "local_government", "federal_government", "government"]),
  not_for_profit: Object.freeze([
    "trade_associations", "charitable_organizations_foundations", "other_not_for_profit",
    "educational_institutions", "health_care"
  ]),
  insurance: Object.freeze([
    "insurance_health", "insurance_property_casualty", "insurance_life", "insurance_other"
  ]),
  commercial: Object.freeze([
    "affordable_housing", "agribusiness", "auto_dealers", "construction", "entertainment",
    "financial_institutions_banking", "hospitality", "manufacturing", "professional_service_firms",
    "real_estate", "retail", "health_care", "oil_gas_distribution", "wholesale", "technology",
    "educational_institutions", "distribution"
  ])
  // `trust_estate` and `business` are deliberately absent — see rule 2. Neither has a stated list, and a
  // trust or an estate can be in any trade at all.
});

// Every industry code this map places somewhere. Anything outside it is a tenant's own addition.
const CLAIMED_INDUSTRY_CODES = new Set(Object.values(REMS_INDUSTRY_BY_ENTITY_TYPE).flat());

/**
 * The Industry options to offer for an entity type, out of the tenant's resolved list.
 *
 * `selected` is the value currently stored, which is always kept — see rule 3 above.
 */
export function remsIndustryOptions (options, entityType, selected = null) {
  // Rule 1 — nothing to offer until the entity type is answered. Rule 4 still holds: a record that
  // somehow carries an industry without an entity type keeps showing the one it has.
  if (!entityType) return options.filter((o) => o.value === selected);
  const allowed = REMS_INDUSTRY_BY_ENTITY_TYPE[entityType];
  if (!allowed) return options;
  return options.filter((o) =>
    allowed.includes(o.value) || !CLAIMED_INDUSTRY_CODES.has(o.value) || o.value === selected);
}

/** Whether an industry is one this entity type offers — what decides if a changed entity type clears it. */
export const remsIndustryFitsEntityType = (entityType, industry) => {
  if (!industry) return true;
  const allowed = REMS_INDUSTRY_BY_ENTITY_TYPE[entityType];
  return !allowed || allowed.includes(industry) || !CLAIMED_INDUSTRY_CODES.has(industry);
};

// EMS form-state codes (RemsFormStatus), for filtering a list by form state. The codes are the server's
// enum names and never change; only the wording below is ours — see the note on the labels.
export const REMS_FORM_STATE_OPTIONS = [
  { label: "Draft", value: "Draft" },
  { label: "Saved", value: "Saved" },
  { label: "Sent", value: "Sent" },
  { label: "Received", value: "Submitted" },
  { label: "Cancelled", value: "Cancelled" }
];

// Whether the client has returned their form — EMS Review's "Form" column. Sent as a string and parsed to
// a bool server-side, because a column filter's value is always a string.
export const REMS_FORM_SUBMITTED_OPTIONS = [
  { label: "Received", value: "true" },
  { label: "Not received", value: "false" }
];

// Approval-task filters (RemsApproverRole / RemsApprovalTaskStatus names, matched server-side).
export const REMS_APPROVER_ROLE_OPTIONS = [
  { label: "Shareholder", value: "Shareholder" },
  { label: "Department Director", value: "DepartmentDirector" },
  { label: "CSE", value: "CSE" },
  { label: "Commission Recipient", value: "CommissionRecipient" },
  { label: "Approver", value: "Approver" }
];

export const REMS_APPROVAL_STATUS_OPTIONS = [
  { label: "Pending", value: "Pending" },
  { label: "Approved", value: "Approved" },
  { label: "Rejected", value: "Rejected" }
];

// One entry per live stage — a missing one falls back to grey, which reads as "draft" on a request that is
// anything but. Awaiting Customer borrows the EMS "Sent" teal (it is the same moment seen from the
// request); the approval stages borrow ENGAGEMENT_STATUS_META's colours, so a request badge and the
// engagement badge underneath it never disagree about what pending/approved looks like; and the two the
// initiator-first rebuild added take the send-back orange and a lighter shade of the admin purple, so a
// badge says both whose desk a request is on and which visit it is.
const STATUS_COLORS = {
  draft: "grey-6",
  awaiting_customer: "teal-7",
  customer_submitted: "deep-purple-6",
  returned_to_initiator: "orange-9",
  awaiting_admin_confirmation: "deep-purple-4",
  pending_approval: "orange-8",
  changes_requested: "negative",
  approved: "positive"
};
// The client's form, seen from the FIRM's side: a form that has come back reads "Received", not
// "Submitted". Submitting is the client's act and it is over; what a member of staff reading a REMS
// surface wants to know is whether the answers are in hand. The code stays `Submitted` — it is the
// server's RemsFormStatus enum name — so only the wording moved.
const EMS_STATE_LABELS = {
  NotStarted: "Not started", Draft: "Draft", Saved: "Saved", Sent: "Sent", Submitted: "Received", Cancelled: "Cancelled"
};
// Colour the EMS form-state chips consistently with the request-status palette.
const EMS_STATE_COLORS = {
  NotStarted: "grey-5", Draft: "grey-6", Saved: "primary", Sent: "teal-7", Submitted: "positive", Cancelled: "negative"
};
const SUBMISSION_STATE_LABELS = { Submitted: "Received", AwaitingCustomer: "Awaiting customer" };

// Approval-task metadata (WO-117 Part B). Approver roles mirror the backend RemsApproverRole enum; the
// task/round status strings mirror RemsApprovalTaskStatus / RemsApprovalRoundStatus.
const APPROVER_ROLE_LABELS = {
  // A holder of the Shareholder role: on every engagement's list by standing, and not removable.
  Shareholder: "Shareholder",
  CSE: "CSE",
  DepartmentDirector: "Department Director",
  CommissionRecipient: "Commission Recipient",
  // A hand-picked approver with no other standing on the engagement (RemsApproverRole.Approver).
  Approver: "Approver"
};
const APPROVER_ROLE_ICONS = {
  Shareholder: "o_workspace_premium",
  CSE: "o_support_agent",
  DepartmentDirector: "o_account_tree",
  CommissionRecipient: "o_payments"
};
// Superseded is a real decision state, not a missing one: the round closed on somebody else's decline
// while this approver still had it open. Without it here the badge fell through to the raw enum name in
// grey, which is the one row on a failed round most in need of saying what happened.
const APPROVAL_STATUS_LABELS = {
  Pending: "Pending", Approved: "Approved", Rejected: "Rejected", Superseded: "No longer required"
};
const APPROVAL_STATUS_COLORS = {
  Pending: "orange-8", Approved: "positive", Rejected: "negative", Superseded: "grey-6"
};

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
const EMAIL_EVENT_LABELS = { Sent: "Sent", Reminder: "Reminder sent", Delivered: "Delivered", Opened: "Opened", Failed: "Failed" };
const EMAIL_EVENT_COLORS = { Sent: "teal-7", Reminder: "amber-8", Delivered: "positive", Opened: "primary", Failed: "negative" };
const EMAIL_EVENT_ICONS = { Sent: "o_send", Reminder: "o_notifications_active", Delivered: "o_mark_email_read", Opened: "o_drafts", Failed: "o_error" };

const labelFrom = (options, value) => options.find((o) => o.value === value)?.label || value || "—";

// The option item's own Description, or "" when it has none — the caller's cue to render no tooltip.
// Unlike labelFrom there is no falling back to the raw value: a code is not an explanation.
const hintFrom = (options, value) => (value ? (options.find((o) => o.value === value)?.description || "") : "");

// Once a request has left its initiator it is with "the admins", which on its own says nothing about the
// one thing anyone wants to know at that stage: has somebody actually taken it? Nobody is named at intake
// any more, so until an admin picks it up the request is nobody's in particular — and that is worth saying
// on the badge rather than leaving a status that reads as though somebody is already on it.
//
// Confined to the stages where an admin is the one expected to act. A request in a rework state is with
// its initiator and is not waiting for anybody to pick anything up, even while unclaimed.
const AWAITING_ADMIN_STATUSES = [REMS_STATUS.ADMIN_REVIEW, REMS_STATUS.AWAITING_ADMIN_CONFIRMATION];
const awaitingPickUp = (row) => AWAITING_ADMIN_STATUSES.includes(row?.status) && !row?.assignedAdmin;

// Label/colour helpers for rendering REMS rows and detail cards. The option-set-backed labels read the
// shared catalogue, so a tenant's rename shows up on every badge and cell rather than only in the picker
// it was edited from; the enum-backed ones are fixed maps because the backend branches on those values.
export function useRemsMeta () {
  const options = useRemsOptionCatalog();
  const auth = useAuthStore();

  const typeLabel = (v) => labelFrom(options.type, v);
  const statusLabel = (v) => labelFrom(options.status, v);
  const referralSourceLabel = (v) => labelFrom(options.referralSource, v);

  // Tooltips come from the option ITEM's Description, maintained in Administration → Option Sets, so a
  // tenant who rewords a value rewords its explanation in the same place. "" when they have written
  // none, which is the caller's cue to render no tooltip rather than an empty box.
  const typeHint = (v) => hintFrom(options.type, v);
  const referralSourceHint = (v) => hintFrom(options.referralSource, v);
  const industryGroupLabel = (v) => labelFrom(options.industryGroup, v);
  const departmentLabel = (v) => labelFrom(options.department, v);
  const subServiceLineLabel = (v) => labelFrom(options.subServiceLine, v);
  const subIndustryLabel = (v) => labelFrom(options.subIndustry, v);

  // Colours stay in code: they key off the CODE, which is closed and validated server-side, so a rename
  // never strands a badge on grey. Only the wording is the tenant's to change.
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

  // Status badge for a request ROW (or detail) rather than a bare code: the status, except that a request
  // sitting with the admins says whether one has actually taken it. Every surface showing a request — EMS
  // Review, the Partner Dashboard, the request detail — uses these, so all three say the same thing about
  // the same request. `row` needs `status` and `assignedAdmin`; a list whose rows name the status
  // differently (EMS Review calls it `requestStatus`) passes a shape rather than its raw row.
  const requestStatusLabel = (row) => (awaitingPickUp(row) ? "Waiting For Pickup" : statusLabel(row?.status));
  const requestStatusColor = (row) => (awaitingPickUp(row) ? "amber-8" : statusColor(row?.status));

  // The EMS engagement/detail action becomes available only once the customer has submitted their
  // form (AC-REMS-002.5 / 005.6); until then it stays disabled.
  const emsDetailAvailable = (row) => row?.clientSubmissionState === "Submitted";

  // Why engagement setup is closed to this user on this row, or null when it is theirs to work.
  // Setup belongs to whoever picked the request up, so an unclaimed request has no owner and someone
  // else's is not yours to take over. The server enforces the same rule — the workspace is a URL — but
  // saying WHY on the button beats letting the click end in a 403. Super Admins and Tenant Admins are
  // exempt from the whole rule there (RemsSetupAccess.IsElevated), so they are exempt here too.
  const isElevated = () => auth.roles.includes("SuperAdmin") || auth.roles.includes("TenantAdmin");
  const engagementOwnerDenial = (row) => {
    if (isElevated()) return null;
    const assignee = row?.assignedAdmin?.id;
    if (!assignee) return "Waiting for pickup — pick this request up to work its engagement setup";
    if (assignee !== auth.user?.userId) {
      return `Picked up by ${row.assignedAdmin?.name || "another Admin"} — only they can work its engagement setup`;
    }
    return null;
  };
  // The email-log / EMS-inbox action is meaningful once a form has been sent to the customer.
  const emsFormActivity = (row) =>
    !!row?.clientSubmissionState || ["Sent", "Submitted"].includes(row?.emsFormState);

  // Live option lists for pickers and column filters. Reactive, so a screen built before the catalogue
  // resolved picks up the tenant's own wording without reloading. The status FILTER carries the queue's
  // extra wording: Admin Review is one code but shows up in a row as two states — waiting for pickup, and
  // picked up — so its label names both rather than just the one somebody happens to be hunting for.
  const typeOptions = computed(() => options.type);
  const referralSourceOptions = computed(() => options.referralSource);
  const statusOptions = computed(() => options.status);
  const industryGroupOptions = computed(() => options.industryGroup);
  const departmentOptions = computed(() => options.department);
  const subServiceLineOptions = computed(() => options.subServiceLine);
  const subIndustryOptions = computed(() => options.subIndustry);
  const statusFilterOptions = computed(() => options.status.map((option) =>
    (option.value === REMS_STATUS.ADMIN_REVIEW
      ? { ...option, label: `${option.label}/Waiting For Pickup` }
      : option)));

  return {
    typeLabel,
    typeHint,
    referralSourceLabel,
    referralSourceHint,
    statusLabel,
    departmentLabel,
    subServiceLineLabel,
    subIndustryLabel,
    statusColor,
    emsStateLabel,
    emsStateColor,
    submissionStateLabel,
    industryGroupLabel,
    typeOptions,
    referralSourceOptions,
    statusOptions,
    industryGroupOptions,
    departmentOptions,
    subServiceLineOptions,
    subIndustryOptions,
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

// The Type picker. Delegates to the shared catalogue rather than resolving its own copy, so
// a screen that shows both a picker and a badge cannot end up with two different labels for one code.
// `load` is kept so existing callers need no change; it just awaits the catalogue.
export function useRemsOptionSets () {
  const catalog = useRemsOptionCatalog();
  return {
    typeOptions: computed(() => catalog.type),
    load: ensureRemsOptionsLoaded
  };
}

// ---- Engagement workspace (WO-117) option sets + conditional logic ----

// Department + Entity Type are stored as string codes, so — like Type — the closed seed lists are a safe
// fallback when the resolve endpoint 403s (the REMS Admin role lacks optionSets.read).
export const REMS_DEPARTMENT_CODES = Object.freeze({ CAS: "cas", TAX: "tax", AUDIT: "audit", GCS: "gcs" });

// The Entity Type code (REMS.IndustryGroup value) that makes an audit a GOVERNMENT audit. Read off the
// entity type rather than anything on the engagement, because it is required and frozen once the
// client's intake form goes out.
export const REMS_ENTITY_TYPE_GOVERNMENT = "government";

export const REMS_DEPARTMENT_OPTIONS = REMS_OPTION_SEED.department;

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
// An audit engagement for a government ENTITY — `entityType` is the request's industryGroup code, which
// lives on the form record rather than on the engagement, so callers pass it in.
export const isGovernmentAudit = (department, entityType) =>
  isAuditDepartment(department) && entityType === REMS_ENTITY_TYPE_GOVERNMENT;

// Loads the engagement's code-valued option sets (Department, Service Line and Industry) plus Marketing /
// Tax Form for the workspace. The code-valued ones degrade to the closed lists in the
// catalogue's seed. Marketing + Tax Form are keyed by OptionSetItem *id* (the
// REMSEngagementMarketingMethod / REMSEngagementTaxForm FKs), so there is no closed fallback for their ids
// — those pickers are simply empty (flagged `*Unavailable`) when resolve is denied.
export function useRemsEngagementOptionSets () {
  // The code-valued lists come from the shared catalogue (the same ones the labels read); Marketing and
  // Tax Form are resolved here because they are keyed by OptionSetItem *id* rather than by code, which is
  // a different shape and only this workspace needs it.
  const catalog = useRemsOptionCatalog();
  const departmentOptions = computed(() => catalog.department);
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
    // Service Line and Industry — still keyed subServiceLine / subIndustry in the data, per the note at
    // the top of this file. Code-valued, so they come from the shared catalogue too.
    subServiceLineOptions: computed(() => catalog.subServiceLine),
    subIndustryOptions: computed(() => catalog.subIndustry),
    // How often the client is billed (REMS.BillingPeriod). Code-valued like Department and Service Line,
    // so it comes from the shared catalogue rather than being resolved by id.
    billingPeriodOptions: computed(() => catalog.billingPeriod),
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
