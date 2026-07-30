import { ref } from "vue";
import { optionSetApi, EntityType } from "services/api";

// The closed REMS.Type / REMS.Priority / REMS.Status codes (mirrors the backend DefaultOptionSets).
// Used both as select options and as label/colour lookups for the dashboard rows and detail cards.
//
// They double as the FALLBACK for the type/priority pickers: the REMS Partner/Admin roles do not
// carry `optionSets.read`, so the option-set `resolve` endpoint 403s for pure-REMS users. The codes
// are a validated closed set server-side (RemsRequestOptionCodes), so falling back to them is safe.
export const REMS_TYPE_OPTIONS = [
  { label: "Brand-New Client", value: "brand_new_client" },
  { label: "New Engagement", value: "new_engagement" },
  { label: "Existing Client", value: "existing_client" },
  { label: "Subsidiary / Child of Existing Client", value: "subsidiary_child_of_existing_client" }
];

export const REMS_PRIORITY_OPTIONS = [
  { label: "Urgent", value: "urgent" },
  { label: "High", value: "high" },
  { label: "Medium", value: "medium" },
  { label: "Low", value: "low" }
];

export const REMS_STATUS_OPTIONS = [
  { label: "Draft", value: "draft" },
  { label: "Submitted", value: "submitted" },
  { label: "Sent", value: "sent" },
  { label: "Awaiting Customer", value: "awaiting_customer" },
  { label: "Customer Submitted", value: "customer_submitted" },
  { label: "Approved", value: "approved" },
  { label: "Rejected", value: "rejected" }
];

// Type codes that mean "an existing client is referenced" (drives the client-lookup type marking).
export const REMS_EXISTING_CLIENT_TYPES = ["existing_client", "subsidiary_child_of_existing_client"];

// Industry Group closed codes (mirror the backend REMS.IndustryGroup option set). The Build-EMS picker
// resolves the tenant-configurable option set first (see useRemsIndustryGroups) and falls back to these
// when the REMS role lacks optionSets.read (403), exactly as the type/priority pickers do.
export const REMS_INDUSTRY_GROUP_OPTIONS = [
  { label: "Individual", value: "individual" },
  { label: "Business", value: "business" },
  { label: "Government", value: "government" }
];

// EMS form-state codes (RemsFormStatus) used to filter the EMS Inbox by form state.
export const REMS_FORM_STATE_OPTIONS = [
  { label: "Draft", value: "Draft" },
  { label: "Saved", value: "Saved" },
  { label: "Sent", value: "Sent" },
  { label: "Submitted", value: "Submitted" },
  { label: "Cancelled", value: "Cancelled" }
];

const PRIORITY_COLORS = { urgent: "red-8", high: "deep-orange-7", medium: "amber-8", low: "blue-grey-5" };
const STATUS_COLORS = {
  draft: "grey-6",
  submitted: "primary",
  sent: "teal-7",
  awaiting_customer: "orange-8",
  customer_submitted: "deep-purple-6",
  approved: "positive",
  rejected: "negative"
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
  CommissionRecipient: "Commission Recipient"
};
const APPROVER_ROLE_ICONS = {
  CSE: "o_support_agent",
  DepartmentDirector: "o_account_tree",
  ManagingShareholder: "o_workspace_premium",
  CommissionRecipient: "o_payments"
};
const APPROVAL_STATUS_LABELS = { Pending: "Pending", Approved: "Approved", Rejected: "Rejected" };
const APPROVAL_STATUS_COLORS = { Pending: "orange-8", Approved: "positive", Rejected: "negative" };

// Provider email-delivery events (RemsFormEmailEventType). These are the ONLY events rendered — the UI
// never synthesises delivery/open state; it shows exactly what the server's email log returns.
const EMAIL_EVENT_LABELS = { Sent: "Sent", Delivered: "Delivered", Opened: "Opened", Failed: "Failed" };
const EMAIL_EVENT_COLORS = { Sent: "teal-7", Delivered: "positive", Opened: "primary", Failed: "negative" };
const EMAIL_EVENT_ICONS = { Sent: "o_send", Delivered: "o_mark_email_read", Opened: "o_drafts", Failed: "o_error" };

const labelFrom = (options, value) => options.find((o) => o.value === value)?.label || value || "—";

// Static label/colour helpers for rendering REMS rows and detail cards.
export function useRemsMeta () {
  const typeLabel = (v) => labelFrom(REMS_TYPE_OPTIONS, v);
  const priorityLabel = (v) => labelFrom(REMS_PRIORITY_OPTIONS, v);
  const statusLabel = (v) => labelFrom(REMS_STATUS_OPTIONS, v);
  const priorityColor = (v) => PRIORITY_COLORS[v] || "grey-6";
  const statusColor = (v) => STATUS_COLORS[v] || "grey-6";
  const emsStateLabel = (v) => EMS_STATE_LABELS[v] || v || "—";
  const emsStateColor = (v) => EMS_STATE_COLORS[v] || "grey-6";
  const submissionStateLabel = (v) => (v ? (SUBMISSION_STATE_LABELS[v] || v) : "—");
  const industryGroupLabel = (v) => labelFrom(REMS_INDUSTRY_GROUP_OPTIONS, v);
  const emailEventLabel = (v) => EMAIL_EVENT_LABELS[v] || v || "—";
  const emailEventColor = (v) => EMAIL_EVENT_COLORS[v] || "grey-6";
  const emailEventIcon = (v) => EMAIL_EVENT_ICONS[v] || "o_mail";
  const approverRoleLabel = (v) => APPROVER_ROLE_LABELS[v] || v || "—";
  const approverRoleIcon = (v) => APPROVER_ROLE_ICONS[v] || "o_person";
  const approvalStatusLabel = (v) => APPROVAL_STATUS_LABELS[v] || v || "—";
  const approvalStatusColor = (v) => APPROVAL_STATUS_COLORS[v] || "grey-6";

  // The EMS engagement/detail action becomes available only once the customer has submitted their
  // form (AC-REMS-002.5 / 005.6); until then it stays disabled.
  const emsDetailAvailable = (row) => row?.clientSubmissionState === "Submitted";
  // The email-log / EMS-inbox action is meaningful once a form has been sent to the customer.
  const emsFormActivity = (row) =>
    !!row?.clientSubmissionState || ["Sent", "Submitted"].includes(row?.emsFormState);

  return {
    typeLabel,
    priorityLabel,
    statusLabel,
    priorityColor,
    statusColor,
    emsStateLabel,
    emsStateColor,
    submissionStateLabel,
    industryGroupLabel,
    emailEventLabel,
    emailEventColor,
    emailEventIcon,
    approverRoleLabel,
    approverRoleIcon,
    approvalStatusLabel,
    approvalStatusColor,
    emsDetailAvailable,
    emsFormActivity
  };
}

// Loads the tenant-configurable REMS.Type / REMS.Priority option lists, falling back to the closed
// codes above when the resolve endpoint is unavailable (e.g. the caller lacks optionSets.read).
export function useRemsOptionSets () {
  const typeOptions = ref(REMS_TYPE_OPTIONS);
  const priorityOptions = ref(REMS_PRIORITY_OPTIONS);

  const resolveInto = async (key, target, fallback) => {
    try {
      const items = await optionSetApi.resolve({ entityType: EntityType.Rems, key });
      target.value = items?.length ? items.map((i) => ({ label: i.label, value: i.value })) : fallback;
    } catch {
      target.value = fallback;
    }
  };

  const load = () => Promise.all([
    resolveInto("REMS.Type", typeOptions, REMS_TYPE_OPTIONS),
    resolveInto("REMS.Priority", priorityOptions, REMS_PRIORITY_OPTIONS)
  ]);

  return { typeOptions, priorityOptions, load };
}

// ---- Engagement workspace (WO-117) option sets + conditional logic ----

// Department + Service Line are stored as string codes, so — like Type/Priority/IndustryGroup — the closed
// seed lists are a safe fallback when the resolve endpoint 403s (the REMS Admin role lacks optionSets.read).
export const REMS_DEPARTMENT_CODES = Object.freeze({ CAS: "cas", TAX: "tax", AUDIT: "audit", GCS: "gcs" });
export const REMS_SERVICE_LINE_GOVERNMENT = "government";

export const REMS_DEPARTMENT_OPTIONS = [
  { label: "CAS", value: "cas" },
  { label: "Tax", value: "tax" },
  { label: "Audit", value: "audit" },
  { label: "GCS", value: "gcs" }
];

export const REMS_SERVICE_LINE_OPTIONS = [
  { label: "Commercial", value: "commercial" },
  { label: "Non-Profit", value: "non_profit" },
  { label: "Government", value: "government" },
  { label: "Individual", value: "individual" }
];

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
  const departmentOptions = ref(REMS_DEPARTMENT_OPTIONS);
  const serviceLineOptions = ref(REMS_SERVICE_LINE_OPTIONS);
  const marketingGroups = ref([]);
  const marketingUnavailable = ref(false);
  const taxFormOptions = ref([]);
  const taxFormUnavailable = ref(false);

  const resolveCodes = async (key, target, fallback) => {
    try {
      const items = await optionSetApi.resolve({ entityType: EntityType.Rems, key });
      target.value = items?.length ? items.map((i) => ({ label: i.label, value: i.value })) : fallback;
    } catch {
      target.value = fallback;
    }
  };

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
    resolveCodes("REMS.Department", departmentOptions, REMS_DEPARTMENT_OPTIONS),
    resolveCodes("REMS.ServiceLine", serviceLineOptions, REMS_SERVICE_LINE_OPTIONS),
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

// Loads the tenant-configurable REMS.IndustryGroup option list for the Build-EMS picker, falling back to
// the closed individual/business/government codes when resolve is unavailable (the REMS roles do not carry
// optionSets.read, so the endpoint 403s for pure-REMS users — mirrors useRemsOptionSets, AC-REMS-007.3).
export function useRemsIndustryGroups () {
  const industryGroupOptions = ref(REMS_INDUSTRY_GROUP_OPTIONS);

  const load = async () => {
    try {
      const items = await optionSetApi.resolve({ entityType: EntityType.Rems, key: "REMS.IndustryGroup" });
      industryGroupOptions.value = items?.length
        ? items.map((i) => ({ label: i.label, value: i.value }))
        : REMS_INDUSTRY_GROUP_OPTIONS;
    } catch {
      industryGroupOptions.value = REMS_INDUSTRY_GROUP_OPTIONS;
    }
  };

  return { industryGroupOptions, load };
}
