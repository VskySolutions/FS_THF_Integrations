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
  // `description` mirrors the option item's own Description column — it is what the tooltip renders
  // wherever the value is offered or displayed, so the seed carries it too and a resolve failure does
  // not silently strip the explanation.
  type: [
    {
      label: "Brand-New Client",
      value: "brand_new_client",
      description: "The client/company is working with THF for the first time. No prior record exists in the system."
    },
    {
      label: "New Engagement, Existing Client",
      value: "existing_client",
      description: "The person or company already has an active client record with THF, and this request " +
        "creates an additional engagement under that same client."
    },
    {
      label: "Subsidiary / Child of Existing Client",
      value: "subsidiary_child_of_existing_client",
      description: "When the client is a child of an already present parent client. All the billing goes " +
        "to the parent client in this situation."
    }
  ],
  // How the client heard about THF, asked on the public EMS form.
  referralSource: [
    { label: "Referral", value: "referral", description: "Friend, Family, or Colleague." },
    { label: "Search Engine", value: "search_engine", description: "Google, Bing, Yahoo." },
    { label: "Digital Ad / Social Media", value: "digital_ad_social", description: "Facebook, Instagram, LinkedIn." },
    {
      label: "Event or Conference",
      value: "event_conference",
      description: "Trade shows, webinars, or local community events."
    },
    {
      label: "Print or Broadcast",
      value: "print_broadcast",
      description: "Direct mailers, billboards, TV, or radio ads."
    },
    {
      label: "Website or Blog",
      value: "website_blog",
      description: "Mentioned in an article, forum (e.g., Reddit), or guest post."
    },
    { label: "Other", value: "other", description: "Anything not covered above." }
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
  // "Business" was split into the three kinds THF onboards; all three ask the same questions the single
  // group did (see REMS_BUSINESS_INDUSTRY_GROUPS in useRemsMeta). `business` is still recognised there
  // for forms sent before the split, it is just no longer offered.
  industryGroup: [
    { label: "Individual", value: "individual" },
    { label: "Not-for-Profit", value: "not_for_profit" },
    { label: "Insurance", value: "insurance" },
    { label: "Commercial", value: "commercial" },
    { label: "Government", value: "government" }
  ],
  department: [
    { label: "CAS", value: "cas" },
    { label: "Tax", value: "tax" },
    { label: "Audit", value: "audit" },
    { label: "GCS", value: "gcs" }
  ],
  // Pairs with the engagement's No. of Bills, which is a plain count rather than anything derived from
  // the period. There is deliberately no "Custom".
  billingPeriod: [
    { label: "Monthly", value: "monthly" },
    { label: "Quarterly", value: "quarterly" },
    { label: "Annual", value: "annual" }
  ],
  serviceLine: [
    { label: "Commercial", value: "commercial" },
    { label: "Non-Profit", value: "non_profit" },
    { label: "Government", value: "government" },
    { label: "Individual", value: "individual" }
  ],
  // One level below the service line: the service actually being sold. Nothing branches on it — the
  // Government Audit rule keys off the LINE — so this is classification only. The Internal-* values are
  // the firm's own work, booked as engagements so the same setup and approval route covers them.
  subServiceLine: [
    { label: "Attest Services", value: "attest_services" },
    { label: "Tax Compliance", value: "tax_compliance" },
    { label: "Client Accounting Services", value: "client_accounting_services" },
    { label: "Outsourced CFO", value: "outsourced_cfo" },
    { label: "Consulting", value: "consulting" },
    { label: "Business Valuation", value: "business_valuation" },
    { label: "IT Services", value: "it_services" },
    { label: "Plan Administration", value: "plan_administration" },
    { label: "Mergers & Acquisitions", value: "mergers_acquisitions" },
    { label: "Payroll Services", value: "payroll_services" },
    { label: "Peer Review", value: "peer_review" },
    {
      label: "SOC",
      value: "soc",
      description: "System and Organization Controls reporting (SOC 1 / SOC 2)."
    },
    { label: "Employee Benefits", value: "employee_benefits" },
    { label: "Estate Planning", value: "estate_planning" },
    { label: "Litigation Support", value: "litigation_support" },
    { label: "Forensic Accounting", value: "forensic_accounting" },
    { label: "Internal-Accounting", value: "internal_accounting" },
    { label: "Internal-Billing", value: "internal_billing" },
    { label: "Internal-Operations", value: "internal_operations" },
    { label: "Internal-Marketing", value: "internal_marketing" },
    { label: "Internal-IT", value: "internal_it" },
    { label: "Internal-Miscellaneous", value: "internal_miscellaneous" }
  ],
  // One level below the industry group: the client's trade. Deliberately NOT filtered by the chosen group
  // — the groups do not partition cleanly (a hospital is Health Care whether it is Commercial or
  // Not-for-Profit), so the whole list is offered and the pairing is the user's to make.
  subIndustry: [
    { label: "Affordable Housing", value: "affordable_housing" },
    { label: "Agribusiness", value: "agribusiness" },
    { label: "Auto Dealers", value: "auto_dealers" },
    { label: "Construction", value: "construction" },
    { label: "Entertainment", value: "entertainment" },
    { label: "Financial Institutions/Banking", value: "financial_institutions_banking" },
    { label: "Hospitality", value: "hospitality" },
    { label: "Manufacturing", value: "manufacturing" },
    { label: "Professional Service Firms", value: "professional_service_firms" },
    { label: "Real Estate", value: "real_estate" },
    { label: "Retail", value: "retail" },
    { label: "Health Care", value: "health_care" },
    { label: "Oil & Gas Distribution", value: "oil_gas_distribution" },
    { label: "Wholesale", value: "wholesale" },
    { label: "Technology", value: "technology" },
    { label: "State Government", value: "state_government" },
    { label: "Local Government", value: "local_government" },
    { label: "Federal Government", value: "federal_government" },
    { label: "Educational Institutions", value: "educational_institutions" },
    { label: "Insurance - Property and Casualty", value: "insurance_property_casualty" },
    { label: "Insurance - Life", value: "insurance_life" },
    { label: "Insurance - Other", value: "insurance_other" },
    { label: "Trade Associations", value: "trade_associations" },
    { label: "Charitable Organizations or Foundations", value: "charitable_organizations_foundations" },
    { label: "Other Not-for-Profit", value: "other_not_for_profit" },
    { label: "Government", value: "government" },
    { label: "Individual", value: "individual" },
    { label: "Distribution", value: "distribution" }
  ]
};

const SET_KEYS = {
  type: "REMS.Type",
  referralSource: "REMS.ReferralSource",
  status: "REMS.Status",
  industryGroup: "REMS.IndustryGroup",
  department: "REMS.Department",
  serviceLine: "REMS.ServiceLine",
  subServiceLine: "REMS.SubServiceLine",
  subIndustry: "REMS.SubIndustry",
  billingPeriod: "REMS.BillingPeriod"
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
      // `description` is the tenant's own explanation of the value, rendered as its tooltip. Null when
      // they have not written one — callers skip the tooltip rather than showing an empty box.
      catalog[name] = items.map((i) => ({ label: i.label, value: i.value, description: i.description || "" }));
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
