import { reactive } from "vue";
import { optionSetApi, EntityType } from "services/api";

// The tenant-configurable REMS option lists, resolved once and shared by every screen.
//
// These are OPTION SETS, not enums: a tenant may rename "Brand-New Client", add a service line, or
// relabel a status in Administration → Option Sets, and every screen has to show what they chose — a badge
// rendered from a hardcoded array would still read the platform default.
//
// The distinction matters the other way too. REMSFormStatus, RemsApproverRole, RemsApprovalTaskStatus and
// RemsEngagementStatus are C# enums the backend branches on; they have no option set and must NOT be
// resolved from one. Their label maps stay in useRemsMeta.
//
// Seeded with the closed codes so the first paint is already right and stays right if a resolve fails
// (a caller without optionSets.read, an offline tick). The resolved set overlays it when it arrives.
const SEED = {
  // Two answers, and only two: every new engagement for a client on file is both "new engagement" and
  // "existing client", and a subsidiary of one is an engagement for a client we already have.
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
  // Stage order and wording, matching the backend RemsRequestStatuses lifecycle. Only the fallback for a
  // caller who cannot resolve the tenant's copy, so it is worth keeping true: a stale seed shows a badge
  // the same user sees differently elsewhere.
  status: [
    { label: "Draft", value: "draft" },
    { label: "Awaiting Customer", value: "awaiting_customer" },
    { label: "Admin Review", value: "customer_submitted" },
    { label: "Returned to Initiator", value: "returned_to_initiator" },
    { label: "Awaiting Admin Confirmation", value: "awaiting_admin_confirmation" },
    { label: "Pending Approval", value: "pending_approval" },
    { label: "Changes Requested", value: "changes_requested" },
    { label: "Approved", value: "approved" }
  ],
  // The three business kinds THF onboards all ask the same questions (see REMS_BUSINESS_INDUSTRY_GROUPS
  // in useRemsMeta), which also still recognises the older `business` code for forms sent under it.
  industryGroup: [
    { label: "Individual", value: "individual" },
    { label: "Not-for-Profit", value: "not_for_profit" },
    { label: "Insurance", value: "insurance" },
    { label: "Commercial", value: "commercial" },
    { label: "Government", value: "government" },
    // A trust or a decedent's estate. In the business family (REMS_BUSINESS_INDUSTRY_GROUPS) because it
    // is asked the same questions: it has an EIN of its own and is acted for by trustees or personal
    // representatives, so the primary / financial / billing contacts are the people who act for it.
    {
      label: "Trust and Estate",
      value: "trust_estate",
      description: "A trust or a decedent's estate. Asked the same questions as a business — it has an " +
        "EIN and is acted for by trustees or personal representatives rather than by an individual."
    }
  ],
  department: [
    { label: "CAS", value: "cas" },
    { label: "Tax", value: "tax" },
    { label: "Audit", value: "audit" },
    { label: "GCS", value: "gcs" },
    // The firm's own internal work. Carries no conditional detail — the audit and tax cards on the setup
    // key off the "audit" and "tax" codes specifically.
    { label: "Admin", value: "admin" }
  ],
  // Pairs with the engagement's Description of Billing Process, which is where a schedule that does not
  // reduce to a frequency gets written out. There is deliberately no "Custom" — that is what the
  // description is for.
  billingPeriod: [
    { label: "Monthly", value: "monthly" },
    { label: "Quarterly", value: "quarterly" },
    { label: "Annual", value: "annual" },
    // Not a frequency: the engagement is billed when a piece of work lands, not when the calendar turns.
    {
      label: "Milestone",
      value: "milestone",
      description: "Billed as each agreed milestone is reached, rather than on a calendar cycle. " +
        "Set out the milestones in the Description of Billing Process."
    }
  ],
  // The service actually being sold, and what the form now calls the SERVICE LINE. The key stays
  // REMS.SubServiceLine (below): every tenant's own copy of the list is keyed by it, and so are the codes
  // already stored on engagements. Nothing branches on this one — it is classification. The Internal-*
  // values are the firm's own work, booked as engagements so the same setup and approval route covers them.
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
  // The client's trade — what the form now calls the INDUSTRY, keyed REMS.SubIndustry for the same reason
  // as the service line above. Deliberately NOT filtered by the chosen entity type: the two do not
  // partition cleanly (a hospital is Health Care whether it is Commercial or Not-for-Profit), so the whole
  // list is offered and the pairing is the user's to make.
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
    // The four insurance trades carry no "Insurance -" prefix: the Industry list is narrowed by the
    // entity type beside it, which already says Insurance. The VALUES keep it — they are the codes
    // engagements are recorded against.
    { label: "Property and Casualty", value: "insurance_property_casualty" },
    { label: "Life", value: "insurance_life" },
    { label: "Other", value: "insurance_other" },
    { label: "Trade Associations", value: "trade_associations" },
    { label: "Charitable Organizations or Foundations", value: "charitable_organizations_foundations" },
    { label: "Other Not-for-Profit", value: "other_not_for_profit" },
    { label: "Government", value: "government" },
    { label: "Individual", value: "individual" },
    { label: "Distribution", value: "distribution" },
    // "Healthcare", one word — deliberately not the same string as "Health Care" above, which is the
    // trade a hospital is in whether it is Commercial or Not-for-Profit. The entity type keeps them apart
    // in the picker; they meet only in the option-set admin.
    { label: "Healthcare", value: "insurance_health" }
  ]
};

const SET_KEYS = {
  type: "REMS.Type",
  referralSource: "REMS.ReferralSource",
  status: "REMS.Status",
  // Three of these read one way in the UI and another here. The KEY is what a tenant's own copy of the
  // list is filed under, so renaming it would orphan theirs and strand the codes stored against it —
  // only the display name moved:
  //   industryGroup   → "Entity Type"
  //   subIndustry     → "Industry"
  //   subServiceLine  → "Service Line"
  industryGroup: "REMS.IndustryGroup",
  department: "REMS.Department",
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
