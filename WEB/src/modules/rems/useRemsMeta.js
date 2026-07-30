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
