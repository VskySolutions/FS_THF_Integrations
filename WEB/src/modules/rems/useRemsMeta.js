import { ref, computed } from "vue";
import { useAuthStore } from "stores/auth";
import { optionSetApi, EntityType } from "services/api";
import { useRemsOptionCatalog, ensureRemsOptionsLoaded } from "modules/rems/useRemsOptionCatalog";
import { REMS_STATUS } from "modules/rems/remsStatus";

// EVERY REMS VALUE IS AN OPTION SET. There is no label, colour, icon or description for any of them in
// this file — all four come from the tenant's own list, resolved once by useRemsOptionCatalog and
// maintained in Administration → Option Sets. Rename a status there and every badge, filter and tooltip
// follows; recolour it and so do the badges.
//
// That includes the lists that mirror a C# enum — form status, approver role, approval decision, approval
// status, engagement status, email event. They are seeded CLOSED: the API refuses to add, delete or
// re-code a value on them, because the server writes those codes and reads them back, and a status
// nothing sets is a status nothing can reach. Everything else about them is the firm's.
//
// What this file holds instead is the RULES: which industries an entity type offers, when a request reads
// as waiting for pickup, when a round is part-signed, what a department is asked. Those are behaviour,
// not wording.
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

// The Entity Type code (REMS.IndustryGroup value) for a person rather than an organisation. It is the
// one entity type asked "Spouse & More Individuals"; every other type is asked "Other Entities" instead.
// Which means it is also what decides how the Related Entities list reads a request: an individual's
// related clients are a FAMILY — a parent and the people on their return — and a company's are simply
// its other entities, with no parent/child relationship captured anywhere.
export const REMS_ENTITY_TYPE_INDIVIDUAL = "individual";

/** Whether a request's entity type is the individual one — see REMS_ENTITY_TYPE_INDIVIDUAL. */
export const isIndividualEntityType = (entityType) => entityType === REMS_ENTITY_TYPE_INDIVIDUAL;

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

// ---------------------------------------------------------------------------------------------------
// THERE ARE NO LABEL, COLOUR OR DESCRIPTION MAPS IN THIS FILE.
//
// Every word, colour and icon a REMS value is rendered with comes from its OPTION SET — the tenant's own
// copy, maintained in Administration → Option Sets. That now includes the seven lists that mirror a C#
// enum (form status, approver role, approval decision, approval status, engagement status, email event,
// client submission): their CODES are the workflow's and the API refuses to add, delete or re-code one,
// but what they are CALLED, what they explain, what colour they are and which icon goes beside them are
// the firm's, exactly like every other list.
//
// So the helpers below all resolve through the catalogue. `optionOf` is the one that matters: it hands
// back the whole option, which is what AppOptionBadge renders.
// ---------------------------------------------------------------------------------------------------

// A code that resolves to nothing. Renders the code itself rather than a blank badge — an unrecognised
// value is worth seeing, and grey with no tooltip says "this is not one of the values on the list".
const unknownOption = (value) => ({
  value,
  label: value || "—",
  description: "",
  backgroundColor: "",
  textColor: "",
  icon: ""
});

/** The full option for a code — label, description, colours and icon — from a resolved list. */
const optionFrom = (options, value) =>
  (value ? options.find((o) => o.value === value) : null) || unknownOption(value);

const labelFrom = (options, value) => optionFrom(options, value).label;

// The option item's own Description, or "" when it has none — the caller's cue to render no tooltip.
// Unlike labelFrom there is no falling back to the raw value: a code is not an explanation.
const hintFrom = (options, value) => (value ? optionFrom(options, value).description : "");

// The REMS.Status value that is NOT a stored status. `customer_submitted` covers both "an admin has this"
// and "nobody has picked it up yet", and the two read very differently to somebody waiting on the
// request — so the badge says which. The application decides WHEN (see awaitingPickUp below); the word,
// the colour and the explanation are on the option like every other value's.
export const REMS_STATUS_WAITING_FOR_PICKUP = "waiting_for_pickup";

// The one value on REMS.ApprovalRoundStatus the enum does not have. A round is Pending from the moment it
// is sent until the last signature, which cannot tell "nobody has looked at this" from "everybody but you
// has signed" — and reading the second as the first is what made an approver's own signature look like
// the request's outcome.
export const REMS_ROUND_PARTIALLY_APPROVED = "partially_approved";

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
  // The stage's own Description, from Administration → Option Sets — what every status badge in REMS now
  // carries as a tooltip. A status is a word about who the request is waiting on and what may be done to
  // it, and the word alone does not say either.
  const statusHint = (v) => hintFrom(options.status, v);

  // Tooltips come from the option ITEM's Description, maintained in Administration → Option Sets, so a
  // tenant who rewords a value rewords its explanation in the same place. "" when they have written
  // none, which is the caller's cue to render no tooltip rather than an empty box.
  const typeHint = (v) => hintFrom(options.type, v);
  const referralSourceHint = (v) => hintFrom(options.referralSource, v);
  const industryGroupLabel = (v) => labelFrom(options.industryGroup, v);
  const departmentLabel = (v) => labelFrom(options.department, v);
  const subServiceLineLabel = (v) => labelFrom(options.subServiceLine, v);
  const subIndustryLabel = (v) => labelFrom(options.subIndustry, v);
  // GCS staffing level. Resolved here because the approver's packet carries the CODE — that screen's
  // option labels are resolved server-side only for the sets keyed by item id (marketing, tax forms).
  const personnelLevelLabel = (v) => labelFrom(options.personnelLevel, v);
  // How often a CAS engagement is billed, resolved for the same reason.
  const billingPeriodLabel = (v) => labelFrom(options.billingPeriod, v);

  // ---- The badges ----
  // Each returns the whole OPTION — label, description, colours, icon — which is what AppOptionBadge
  // renders. Nothing below decides how a value looks; it only decides WHICH value applies.
  const formStatusOption = (v) => optionFrom(options.formStatus, v);
  const submissionStateOption = (v) => optionFrom(options.clientSubmissionState, v);
  const approverRoleOption = (v) => optionFrom(options.approverRole, v);
  const approvalStatusOption = (v) => optionFrom(options.approvalStatus, v);
  const engagementStatusOption = (v) => optionFrom(options.engagementStatus, v);
  const emailEventOption = (v) => optionFrom(options.emailEvent, v);
  // How far one related client has got. Rendered as a badge AND offered as the choices behind it — the
  // status moves only by hand, so the same option is what the row shows and what the dropdown sets.
  const relatedEntityStatusOption = (v) => optionFrom(options.relatedEntityStatus, v);
  // What kind of entity the client is. A badge as well as a word now: it is what decides which questions
  // the client's intake asked, so on a list about what their intake produced it is a category worth
  // seeing rather than reading. Six hues rather than a ramp — see DefaultOptionSets.
  const industryGroupOption = (v) => optionFrom(options.industryGroup, v);

  // The label-only forms, for a table column's `field` (which sorts and searches on a string) and for the
  // few places a value is read as plain text rather than as a badge.
  const emsStateLabel = (v) => labelFrom(options.formStatus, v);
  const submissionStateLabel = (v) => (v ? labelFrom(options.clientSubmissionState, v) : "—");
  const approverRoleLabel = (v) => labelFrom(options.approverRole, v);
  const approvalStatusLabel = (v) => labelFrom(options.approvalStatus, v);

  /**
   * Where a whole approval ROUND stands, refined by how many of its approvers have signed.
   *
   * The counts only ever change the PENDING case: a round that has closed is closed whatever the tally,
   * and "3 of 4" on a declined round would read as progress towards an approval that is never coming.
   * A part-signed one resolves to the list's own `partially_approved` value, so the wording and the
   * colour are the firm's; the tally is prepended to their description because it is data, not wording.
   */
  const roundStatusOption = (status, approved = 0, total = 0) => {
    const partial = status === "Pending" && total > 0 && approved > 0;
    const option = optionFrom(options.approvalRoundStatus, partial ? REMS_ROUND_PARTIALLY_APPROVED : status);
    if (!total) return option;
    const tally = `${approved} of ${total} approvers have signed.`;
    return { ...option, description: [tally, option.description].filter(Boolean).join(" ") };
  };

  // Status badge for a request ROW (or detail) rather than a bare code: the status, except that a request
  // sitting with the admins says whether one has actually taken it. Every surface showing a request — EMS
  // Review, the Partner Dashboard, the request detail — uses this, so all three say the same thing about
  // the same request. `row` needs `status` and `assignedAdmin`; a list whose rows name the status
  // differently (EMS Review calls it `requestStatus`) passes a shape rather than its raw row.
  //
  // "Waiting for pickup" is a value on REMS.Status like any other — see REMS_STATUS_WAITING_FOR_PICKUP.
  // The only thing decided here is WHEN it applies.
  const requestStatusOption = (row) =>
    optionFrom(options.status, awaitingPickUp(row) ? REMS_STATUS_WAITING_FOR_PICKUP : row?.status);
  const requestStatusLabel = (row) => requestStatusOption(row).label;

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
  //
  // "Waiting For Pickup" is dropped from the FILTER: it is a value on the list, but not one any request
  // is stored under, so filtering by it would match nothing. Admin Review carries both states instead,
  // named after the value rather than after a hardcoded string — a firm that renames either one sees
  // their own words here.
  const statusFilterOptions = computed(() => {
    const pickup = options.status.find((o) => o.value === REMS_STATUS_WAITING_FOR_PICKUP);
    return options.status
      .filter((o) => o.value !== REMS_STATUS_WAITING_FOR_PICKUP)
      .map((option) => (option.value === REMS_STATUS.ADMIN_REVIEW && pickup
        ? { ...option, label: `${option.label}/${pickup.label}` }
        : option));
  });

  // The approval-decision filter on the Approvals inbox, from the same list its badges are rendered from.
  const approvalStatusFilterOptions = computed(() => options.approvalStatus);

  // The Related Entities list's status column: the same list drives the dropdown on every row and the
  // filter in the drawer, so a firm that adds a fifth position can both set it and filter by it.
  const relatedEntityStatusOptions = computed(() => options.relatedEntityStatus);

  return {
    typeLabel,
    typeHint,
    referralSourceLabel,
    referralSourceHint,
    statusLabel,
    statusHint,
    departmentLabel,
    subServiceLineLabel,
    subIndustryLabel,
    personnelLevelLabel,
    billingPeriodLabel,
    industryGroupLabel,
    typeOptions,
    referralSourceOptions,
    statusOptions,
    industryGroupOptions,
    departmentOptions,
    subServiceLineOptions,
    subIndustryOptions,
    statusFilterOptions,
    approvalStatusFilterOptions,
    relatedEntityStatusOptions,
    // The badges: each hands back the whole option, for AppOptionBadge.
    requestStatusOption,
    formStatusOption,
    submissionStateOption,
    approverRoleOption,
    approvalStatusOption,
    roundStatusOption,
    engagementStatusOption,
    emailEventOption,
    relatedEntityStatusOption,
    industryGroupOption,
    // …and the label-only forms, for a column's sort key or a line of plain text.
    requestStatusLabel,
    emsStateLabel,
    submissionStateLabel,
    approverRoleLabel,
    approvalStatusLabel,
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
export const REMS_DEPARTMENT_CODES = Object.freeze({
  CAS: "cas", TAX: "tax", AUDIT: "audit", GCS: "gcs", ASSURANCE: "assurance", ADMIN: "admin"
});

// The Entity Type code (REMS.IndustryGroup value) that makes an audit a GOVERNMENT audit. Read off the
// entity type rather than anything on the engagement, because it is required and frozen once the
// client's intake form goes out.
export const REMS_ENTITY_TYPE_GOVERNMENT = "government";

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

// Client Accounting Services. The one department billed on a schedule of its own — a recurring
// arrangement whose frequency and process are part of the engagement — so it is the only one asked for a
// Billing Frequency and a Description of Billing Process. The other departments bill against the work,
// and the two boxes sat empty on every one of their engagements.
//
// Unlike the three above this has NO backend twin: it decides what the setup form asks for, not what the
// API stores. The columns stay on the engagement for every department, so a value recorded under CAS
// survives a department correction and comes back if the department is put back — see saveSetup, which
// omits the pair rather than blanking it.
export const isCasDepartment = (department) => department === REMS_DEPARTMENT_CODES.CAS;

// Attest work priced for the engagement rather than for its first year. Asked the signed client-acceptance
// form Audit is, plus the client fiscal year end and the administrative fees. A department in its own
// right beside Audit, not a rename of it — engagements already filed under `audit` stay there.
export const isAssuranceDepartment = (department) => department === REMS_DEPARTMENT_CODES.ASSURANCE;

// Government Consulting Services: set up against a purchase order rather than a fee.
export const isGcsDepartment = (department) => department === REMS_DEPARTMENT_CODES.GCS;

// The departments asked for a signed client-acceptance form. Mirrors
// RemsEngagementCodes.RequiresClientAcceptanceForm, which is what actually gates the approval.
export const requiresClientAcceptanceForm = (department) =>
  isAuditDepartment(department) || isAssuranceDepartment(department);

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
    // How a GCS engagement is staffed (REMS.PersonnelLevel) — code-valued, so likewise from the catalogue.
    personnelLevelOptions: computed(() => catalog.personnelLevel),
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
