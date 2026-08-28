import { reactive } from "vue";
import { optionSetApi, EntityType } from "services/api";

// The tenant-configurable REMS option lists, resolved once and shared by every screen.
//
// These are OPTION SETS: a tenant may rename "Brand-New Client", add a service line, or relabel a status
// in Administration → Option Sets, and every screen has to show what they chose — a badge rendered from a
// hardcoded array would still read the platform default.
//
// That includes the seven lists mirroring a C# enum (form status, approver role, approval decision and
// the rest). They are CLOSED, not static: the API refuses to add, delete or re-code a value on them
// because the server writes those codes and reads them back — but the label, description, colour and icon
// on each one are the tenant's, maintained on the same screen as every other list.
//
// There is NO hardcoded copy of any list here. There used to be: a ~250-line `SEED` mirroring the
// server's DefaultOptionSets, which every screen painted from until the resolve arrived and which stood
// in whenever the resolve 403'd. It was a second source of truth for words a tenant owns, and it drifted
// from the lists they actually edit — the exact failure option sets exist to prevent. /option-sets/resolve
// is readable by any authenticated caller for that reason, so nothing is left for a local copy to do.

const SET_KEYS = {
  type: "REMS.Type",
  referralSource: "REMS.ReferralSource",
  status: "REMS.Status",
  // The seven lists below mirror C# enums the workflow branches on. They are option sets all the same:
  // their CODES are fixed (the API refuses to add, delete or re-code a value on a closed list) but the
  // label, the description behind its tooltip, the colour and the icon are the tenant's, maintained in
  // Administration → Option Sets like every other list here.
  //
  // Before this they were hardcoded maps in useRemsMeta, which is why a firm that calls a Shareholder a
  // Principal had nowhere to say so.
  formStatus: "REMS.FormStatus",
  clientSubmissionState: "REMS.ClientSubmissionState",
  approverRole: "REMS.ApproverRole",
  approvalStatus: "REMS.ApprovalStatus",
  approvalRoundStatus: "REMS.ApprovalRoundStatus",
  engagementStatus: "REMS.EngagementStatus",
  emailEvent: "REMS.EmailEvent",
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
  billingPeriod: "REMS.BillingPeriod",
  personnelLevel: "REMS.PersonnelLevel"
};

// Module-level and shared: these lists are per-tenant and change rarely, so resolving them once beats
// every screen fetching its own copy.
// Every list starts EMPTY and is filled from the API. Until it arrives, optionFrom() in useRemsMeta
// renders the raw code rather than a blank — degraded for one paint, and never a wrong word.
const catalog = reactive(Object.fromEntries(Object.keys(SET_KEYS).map((k) => [k, []])));

let loading = null;

// Which lists actually came back. A list that FAILED is not the same as one a tenant emptied, and the
// difference is the whole reason this set exists: without it, one bad first attempt was permanent.
//
// `loading` is module state, so it outlives every REMS screen. Caching the first attempt whatever it
// returned meant a single hiccup on the first REMS page of a session — a token still settling, a cold
// API, one dropped connection out of sixteen fired at once — left EVERY badge in that session showing its
// raw code (`awaiting_customer` rather than "Awaiting Customer"), with nothing to retry it. A hard
// refresh fixed it because a hard refresh is a retry: new module, `loading` back to null. Nothing short
// of one was.
const resolved = new Set();
const ALL_KEYS = Object.keys(SET_KEYS);

const resolveOne = async (name) => {
  try {
    const items = await optionSetApi.resolve({ entityType: EntityType.Rems, key: SET_KEYS[name] });
    // An empty set means the tenant emptied the list, not that resolution failed. Assigned either way:
    // an emptied list IS the tenant's answer, and there is no longer a local copy to prefer over it.
    if (items) {
      // Everything a screen needs to RENDER the value, carried on the option itself: what it is called,
      // what it means, what colour its badge is and which icon goes beside it. All four are the tenant's
      // to change, and no screen holds a copy of any of them any more.
      //
      // `description` is null when nobody has written one — callers skip the tooltip rather than showing
      // an empty box. The colours are null on the lists that are not badges (a service line is a word in
      // a picker), and AppOptionBadge falls back to a neutral grey for those.
      catalog[name] = items.map((i) => ({
        label: i.label,
        value: i.value,
        description: i.description || "",
        backgroundColor: i.backgroundColor || "",
        textColor: i.textColor || "",
        icon: i.icon || ""
      }));
    }
    resolved.add(name);
  } catch (err) {
    // Network or auth failure. The list stays as it was — empty on first load, or the last good resolve
    // on a later one — and is deliberately NOT marked resolved, so the next screen asks for it again.
    //
    // Said out loud rather than swallowed. /option-sets/resolve needs only authentication, so a failure
    // here is not routine, and its only visible symptom is a badge reading its own code — which looks
    // like a rendering bug and sends the reader nowhere near the request that actually failed.
    console.warn(`[REMS] Could not resolve the "${SET_KEYS[name]}" option list; badges will show raw codes until it loads.`, err);
  }
};

// Only the lists still missing: a retry after a partial failure re-asks for those and leaves the ones
// already in hand alone.
const loadAll = async () => {
  await Promise.all(ALL_KEYS.filter((k) => !resolved.has(k)).map(resolveOne));
  // A load that did not resolve every list is not one to remember. Cleared AFTER the await, so callers
  // that arrived mid-flight still share this attempt and only the NEXT one starts again.
  if (resolved.size !== ALL_KEYS.length) {
    loading = null;
  }
};

/**
 * Resolves the lists once per session; safe to call from every screen.
 *
 * Called from `useRemsOptionCatalog()`, so every REMS component's setup is a retry point for whatever is
 * still missing. That is what makes a transient failure cost one screen rather than the whole session.
 */
export function ensureRemsOptionsLoaded () {
  loading ??= loadAll();
  return loading;
}

/** Re-resolves after a tenant switch — the lists are tenant-owned copies, so every one is stale. */
export function reloadRemsOptions () {
  resolved.clear();
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
