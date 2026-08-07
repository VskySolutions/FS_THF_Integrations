import { reactive } from "vue";
import { optionSetApi, EntityType } from "services/api";

// The tenant-configurable REMS option lists, resolved once and shared by every screen.
//
// These are OPTION SETS, not enums: a tenant may rename "Brand-New Client", add a service line, or
// relabel a status in Administration → Option Sets, and the app has to show what they chose. Screens used
// to render the seeded English labels from a hardcoded array instead, so those edits changed the pickers
// and nothing else — a row's badge still read the platform default.
//
// The distinction matters the other way too. REMSFormStatus, RemsApproverRole, RemsApprovalTaskStatus and
// RemsEngagementStatus are C# enums the backend branches on; they have no option set and must NOT be
// resolved from one. Their label maps stay in useRemsMeta.
//
// Seeded with the closed codes so the first paint is already right and stays right if a resolve fails
// (a caller without optionSets.read, an offline tick). The resolved set overlays it when it arrives.
const SEED = {
  // "New Engagement" and "Existing Client" were merged into one value — every new engagement for a
  // client we already have is both — keeping the `existing_client` code (MergeRemsExistingClientTypes).
  type: [
    { label: "Brand-New Client", value: "brand_new_client" },
    { label: "New Engagement, Existing Client", value: "existing_client" },
    { label: "Subsidiary / Child of Existing Client", value: "subsidiary_child_of_existing_client" }
  ],
  priority: [
    { label: "Urgent", value: "urgent" },
    { label: "High", value: "high" },
    { label: "Medium", value: "medium" },
    { label: "Low", value: "low" }
  ],
  // Stage order, matching the backend RemsRequestStatuses lifecycle.
  status: [
    { label: "Draft", value: "draft" },
    { label: "Submitted", value: "submitted" },
    { label: "Awaiting Customer", value: "awaiting_customer" },
    { label: "Engagement Setup", value: "customer_submitted" },
    { label: "Pending Approval", value: "pending_approval" },
    { label: "Changes Requested", value: "changes_requested" },
    { label: "Approved", value: "approved" }
  ],
  industryGroup: [
    { label: "Individual", value: "individual" },
    { label: "Business", value: "business" },
    { label: "Government", value: "government" }
  ],
  department: [
    { label: "CAS", value: "cas" },
    { label: "Tax", value: "tax" },
    { label: "Audit", value: "audit" },
    { label: "GCS", value: "gcs" }
  ],
  serviceLine: [
    { label: "Commercial", value: "commercial" },
    { label: "Non-Profit", value: "non_profit" },
    { label: "Government", value: "government" },
    { label: "Individual", value: "individual" }
  ]
};

const SET_KEYS = {
  type: "REMS.Type",
  priority: "REMS.Priority",
  status: "REMS.Status",
  industryGroup: "REMS.IndustryGroup",
  department: "REMS.Department",
  serviceLine: "REMS.ServiceLine"
};

// Module-level and shared: these lists are per-tenant and change rarely, so resolving them once beats
// every screen fetching its own copy.
const catalog = reactive(Object.fromEntries(Object.entries(SEED).map(([k, v]) => [k, [...v]])));

let loading = null;

const resolveOne = async (name) => {
  try {
    const items = await optionSetApi.resolve({ entityType: EntityType.Rems, key: SET_KEYS[name] });
    // An empty set means the tenant emptied the list, not that resolution failed — but rendering nothing
    // would leave every badge blank, so the seed stands in.
    if (items?.length) {
      catalog[name] = items.map((i) => ({ label: i.label, value: i.value }));
    }
  } catch {
    // Keep the seed. A caller without optionSets.read still sees correct default labels.
  }
};

const loadAll = () => Promise.all(Object.keys(SET_KEYS).map(resolveOne));

/** Resolves the lists once per session; safe to call from every screen. */
export function ensureRemsOptionsLoaded () {
  loading ??= loadAll();
  return loading;
}

/** Re-resolves after a tenant switch — the lists are tenant-owned copies. */
export function reloadRemsOptions () {
  loading = loadAll();
  return loading;
}

if (typeof window !== "undefined") {
  window.addEventListener("tenant-switched", () => { reloadRemsOptions(); });
}

export function useRemsOptionCatalog () {
  ensureRemsOptionsLoaded();
  return catalog;
}

/** The seeded codes, for the rare caller that needs the closed set rather than the tenant's edit of it. */
export const REMS_OPTION_SEED = SEED;
