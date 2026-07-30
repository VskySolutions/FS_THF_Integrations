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
const SUBMISSION_STATE_LABELS = { Submitted: "Submitted", AwaitingCustomer: "Awaiting customer" };

const labelFrom = (options, value) => options.find((o) => o.value === value)?.label || value || "—";

// Static label/colour helpers for rendering REMS rows and detail cards.
export function useRemsMeta () {
  const typeLabel = (v) => labelFrom(REMS_TYPE_OPTIONS, v);
  const priorityLabel = (v) => labelFrom(REMS_PRIORITY_OPTIONS, v);
  const statusLabel = (v) => labelFrom(REMS_STATUS_OPTIONS, v);
  const priorityColor = (v) => PRIORITY_COLORS[v] || "grey-6";
  const statusColor = (v) => STATUS_COLORS[v] || "grey-6";
  const emsStateLabel = (v) => EMS_STATE_LABELS[v] || v || "—";
  const submissionStateLabel = (v) => (v ? (SUBMISSION_STATE_LABELS[v] || v) : "—");

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
    submissionStateLabel,
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
