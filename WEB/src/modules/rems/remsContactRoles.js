// The client-intake contact roles, in ONE place. Three surfaces render them — the public form, its
// review step, and the submitted-form panel staff read — and they had a private copy each, which is how
// the panel came to be listing roles the form had stopped asking for.
//
// The keys are the payload's own (RemsRolesPayload), so what is written here is what is stored.

/// The label each role is asked and read under. Named for what the firm needs from the person rather
/// than for the office they hold: not every client has a CEO or a CFO, and a two-partner practice asked
/// for both was left guessing which of them to put where.
export const CONTACT_ROLE_LABELS = {
  self: "Self",
  spouse: "Spouse",
  primaryContact: "Primary Client Contact",
  financialContact: "Financial Contact",
  billingContact: "Billing Contact",
  otherContact: "Other Contact",
  financeDirector: "Finance Director",

  // Retired. Kept so a submission that carries one still says what it is — a client's banker and lawyer
  // are their advisers rather than the firm's contacts on the engagement, and both boxes were left blank
  // on almost every form.
  banker: "Banker",
  lawyer: "Lawyer"
};

/// A one-line note on each role, for the tooltip beside it. Only where the label leaves a real question:
/// "Self" and "Spouse" explain themselves.
export const CONTACT_ROLE_HINTS = {
  primaryContact: "Who we speak to about this engagement — the main person on your side.",
  financialContact: "Who we speak to about your finances and reporting.",
  billingContact: "Who receives our invoices.",
  otherContact: "Anyone else you would like us to have on file.",
  financeDirector: "The finance director for this entity."
};

/// The keys the payload can carry, in the order they are asked. Used to seed and to iterate a payload
/// whose group is not known (a submission rendered before its entity type is read).
export const ALL_ROLE_KEYS = [
  "self", "spouse",
  "primaryContact", "financialContact", "billingContact", "otherContact",
  "financeDirector",
  "banker", "lawyer"
];

/// What each industry group is asked, in display order. The three business groups share one set, so they
/// all look up under "business" (see groupKey below).
export const GROUP_ROLES = {
  individual: ["self", "spouse"],
  business: ["primaryContact", "financialContact", "billingContact", "otherContact"],
  government: ["financeDirector", "billingContact", "otherContact"]
};

/// Which of them must be filled in. Mirrors RemsFormPayloadValidator.
export const REQUIRED_ROLES = {
  individual: ["self"],
  business: ["primaryContact", "financialContact", "billingContact"],
  government: ["financeDirector"]
};

/// The payload keys these roles used to be stored under. Read, never written: a client part-way through
/// a form filled in under the old names must not lose the contacts they already typed, and a submission
/// is the immutable record of what they sent. Mirrors RemsRolesPayload.Normalized on the server.
export const LEGACY_ROLE_ALIASES = {
  ceo: "primaryContact",
  cfo: "financialContact",
  accountsPayable: "billingContact"
};

const hasAny = (role) =>
  !!role && [role.firstName, role.lastName, role.name, role.email, role.phone]
    .some((v) => v != null && String(v).trim() !== "");

/**
 * A roles node with the legacy keys folded into their successors, so everything downstream reads one
 * shape. A payload carrying both keeps the current one — it was written later, by a form that offered
 * the legacy answer nowhere.
 */
export const normalizeRoles = (roles) => {
  const out = { ...(roles || {}) };
  Object.entries(LEGACY_ROLE_ALIASES).forEach(([legacy, current]) => {
    if (!hasAny(out[current]) && hasAny(out[legacy])) out[current] = out[legacy];
    delete out[legacy];
  });
  return out;
};

/**
 * A contact's name as one string: the two boxes joined, falling back to the single `name` a payload
 * saved before the split carries.
 */
export const roleDisplayName = (role) => {
  const joined = [role?.firstName, role?.lastName]
    .filter((v) => v != null && String(v).trim() !== "")
    .map((v) => String(v).trim())
    .join(" ");
  return joined || String(role?.name ?? "").trim();
};

export const roleHasAny = hasAny;

/** Which role set an industry group is asked. The three business groups share one. */
export const groupKey = (industryGroup, isBusiness) => (isBusiness ? "business" : industryGroup);

/**
 * The roles to render for a group, as [{ key, label, hint, required }]. `extraKeys` adds any role the
 * payload carries that the group no longer asks for — a retired Banker on an older submission — so a
 * record shows what is in it rather than what the current form would have collected.
 */
export const roleDefsFor = (key, extraKeys = []) => {
  const order = GROUP_ROLES[key] || [];
  const required = REQUIRED_ROLES[key] || [];
  const extras = extraKeys.filter((k) => !order.includes(k));
  return [...order, ...extras].map((roleKey) => ({
    key: roleKey,
    label: CONTACT_ROLE_LABELS[roleKey] || roleKey,
    hint: CONTACT_ROLE_HINTS[roleKey] || "",
    required: required.includes(roleKey)
  }));
};

/** The keys a payload actually carries an answer under — what drives `extraKeys` above. */
export const answeredRoleKeys = (roles) =>
  ALL_ROLE_KEYS.filter((k) => hasAny(roles?.[k]));

/**
 * The generational suffixes offered beside a client's name. Suggestions, not a closed list: these are
 * what most clients need, not all a client may have, so the field itself stays free text.
 */
export const CLIENT_NAME_SUFFIXES = [
  { value: "Jr.", label: "Jr.", caption: "Junior" },
  { value: "Sr.", label: "Sr.", caption: "Senior" },
  { value: "II", label: "II", caption: "The second" },
  { value: "III", label: "III", caption: "The third" },
  { value: "IV", label: "IV", caption: "The fourth" }
];

/** A client's name as it reads — the name with its suffix on the end. Mirrors REMS.ClientDisplayName. */
export const clientDisplayName = (name, suffix) =>
  [String(name ?? "").trim(), String(suffix ?? "").trim()].filter(Boolean).join(" ");
