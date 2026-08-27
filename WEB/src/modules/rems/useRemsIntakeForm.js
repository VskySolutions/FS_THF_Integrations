import { reactive } from "vue";
import { blankAddress, toAddress, fromAddress, addressComplete } from "modules/rems/remsAddress";
import {
  ALL_ROLE_KEYS, GROUP_ROLES, groupKey, normalizeRoles, roleDefsFor
} from "modules/rems/remsContactRoles";
// Only the group predicate, which is a plain frozen list. useRemsMeta reaches for the auth store inside
// its composable, never at import time — this form renders on an anonymous page and must not wake one.
import { isBusinessIndustryGroup } from "modules/rems/useRemsMeta";
import { nameIssue } from "utils/personName";

// The client intake form's DATA, in one place — its shape, how a stored payload is read into it, how it
// is written back out, and what still has to be filled in before it can be sent.
//
// Two screens hold this form now: the client's own public page, and the dialog an Admin corrects their
// answers in. They are different hosts — one auto-saves against an invite code and walks a review step,
// the other opens over a request and saves once — but the form between them is the same form, and a
// second copy of six hundred lines of shape-and-validation is a copy that drifts. ClientIntakeFields
// renders it; this module knows what is in it.
//
// The addresses are the one place the two shapes differ. The payload travels in the frozen REMS wire
// names (street / zip / countryCode …) and AppAddressFields binds the canonical model, so toAddress() on
// the way in and fromAddress() on the way out — see modules/rems/remsAddress, which is the only module
// that knows both.

const s = (v) => (v == null ? "" : String(v));
const filled = (v) => !!String(v ?? "").trim();
const emailOk = (v) => /^\S+@\S+\.\S+$/.test(String(v ?? "").trim());
const dateOrNull = (v) => (filled(v) ? v : null);
// Adds a validator's complaint to the issue list, and nothing at all when it had none.
const pushIf = (out, issue) => { if (issue) out.push(issue); };

const blankRole = () => ({ prefix: "", firstName: "", lastName: "", email: "", phone: "" });
const blankRoles = () => Object.fromEntries(ALL_ROLE_KEYS.map((k) => [k, blankRole()]));

/**
 * A blank editable payload — the RemsFormPayloadV1 camelCase wire shape (field names match
 * RemsPublicFormModels.cs exactly), EXCEPT the three addresses, which are held canonically.
 */
export const blankIntakePayload = () => reactive({
  version: 1,
  // The client's name, in one field and in two. An individual fills the two and `clientName` is built
  // from them on the way out; a business or government body fills `clientName` and leaves the two blank.
  clientName: "",
  // The title an individual asked to be addressed by. Held apart from the name, and deliberately not
  // folded into it: the name is what THF files and searches the client under.
  clientPrefix: "",
  clientFirstName: "",
  clientLastName: "",
  email: "",            // LOCKED (to the request's customer email; ignored on submit)
  mobileNumber: "",
  referralSource: "",
  referralSourceDetail: "",
  physicalAddress: blankAddress(),
  mailingAddress: blankAddress(),
  billingContactName: "",
  billingEmail: "",
  billingAddress: blankAddress(),
  spouseName: "",
  spousePhone: "",
  spouseEmail: "",
  ein: "",
  contractStartDate: "",
  contractEndDate: "",
  originalTerm: "",
  renewalTerms: "",
  poStartDate: "",
  poEndDate: "",
  roles: blankRoles(),
  relatedEntities: []   // [{ sourceKey, fullName, emailAddress, phoneNumber }]
});

/** True when a canonical address has anything in it — what decides if there is something worth copying. */
export const addressHasContent = (address) =>
  !!address && Object.values(address).some((v) => typeof v === "string" && v.trim() !== "");

/**
 * Copy one address into another, ONCE. Deliberately not a live mirror: the client can correct the copy
 * afterwards, and a later edit to the source must not silently drag the copy along with it — a live
 * mirror would move the billing address every time the physical one was corrected.
 *
 * Assigned field by field into the EXISTING object rather than replacing it, so the bound model instance
 * survives (AppAddressFields resolves its country → state → city cascade from that instance).
 */
export const copyIntakeAddress = (payload, fromKey, toKey) => {
  Object.assign(payload[toKey], { ...payload[fromKey] });
};

// A contact answered at all. `prefix` is deliberately not counted — see roleHasAny in remsContactRoles.
const roleAny = (r) =>
  filled(r?.firstName) || filled(r?.lastName) || filled(r?.name) || filled(r?.email) || filled(r?.phone);

// A payload written before the name was two boxes carries `name` alone. It is accepted as it stands
// rather than asking somebody to retype a name they already gave. Mirrors
// RemsFormPayloadValidator.ValidateRoleFields.
const rolePreSplit = (r) => !filled(r?.firstName) && !filled(r?.lastName) && filled(r?.name);
// Phone is captured when known but never required — a contact is a name and a valid email.
const roleComplete = (r) =>
  (rolePreSplit(r) || (filled(r?.firstName) && filled(r?.lastName))) && emailOk(r?.email);

/** A draft saved before the name was split keeps its single `name` until somebody edits the two boxes. */
function fillRole (target, src) {
  target.prefix = src?.prefix ?? "";
  target.firstName = src?.firstName ?? "";
  target.lastName = src?.lastName ?? "";
  target.name = src?.firstName || src?.lastName ? "" : (src?.name ?? "");
  target.email = src?.email ?? "";
  target.phone = src?.phone ?? "";
}

function makeEntity (e, i) {
  return {
    sourceKey: e?.sourceKey || `related-${Date.now()}-${i}`,
    fullName: e?.fullName ?? "",
    emailAddress: e?.emailAddress ?? "",
    phoneNumber: e?.phoneNumber ?? ""
  };
}

/** A fresh, empty related-entity row. */
export const newRelatedEntity = (index = 0) => ({
  sourceKey: `related-${Date.now()}-${index}`,
  fullName: "",
  emailAddress: "",
  phoneNumber: ""
});

/**
 * Read a stored payload into the editable one, in place.
 *
 * `prefill` is the public form's locked intake data (the name staff typed and the address the invite was
 * sent to); the Admin's correction dialog passes none, because the stored payload IS the answer. `email`
 * always wins from the prefill where there is one — it is the address the invite went to.
 */
export function seedIntakePayload (payload, stored, prefill = null) {
  const d = stored || {};

  payload.clientName = d.clientName ?? prefill?.clientName ?? "";
  payload.clientPrefix = d.clientPrefix ?? "";
  // The two parts come from the stored answer where they were given, and from the prefill's own split of
  // the name staff typed at intake where they were not. `?? ""` rather than a fallback chain into
  // clientName: a business's single name is not a first name, and prefilling one into that box would put
  // "Acme Holdings" where a given name goes the moment somebody switched an entity type.
  payload.clientFirstName = d.clientFirstName ?? prefill?.clientFirstName ?? "";
  payload.clientLastName = d.clientLastName ?? prefill?.clientLastName ?? "";
  payload.email = prefill?.email ?? d.email ?? "";
  payload.mobileNumber = d.mobileNumber ?? prefill?.mobileNumber ?? "";
  payload.referralSource = d.referralSource ?? "";
  payload.referralSourceDetail = d.referralSourceDetail ?? "";
  payload.billingContactName = d.billingContactName ?? "";
  payload.billingEmail = d.billingEmail ?? "";
  payload.spouseName = d.spouseName ?? "";
  payload.spousePhone = d.spousePhone ?? "";
  payload.spouseEmail = d.spouseEmail ?? "";
  payload.ein = d.ein ?? "";
  payload.originalTerm = d.originalTerm ?? "";
  payload.renewalTerms = d.renewalTerms ?? "";
  payload.contractStartDate = d.contractStartDate ?? "";
  payload.contractEndDate = d.contractEndDate ?? "";
  payload.poStartDate = d.poStartDate ?? "";
  payload.poEndDate = d.poEndDate ?? "";

  payload.physicalAddress = toAddress(d.physicalAddress);
  payload.mailingAddress = toAddress(d.mailingAddress);
  payload.billingAddress = toAddress(d.billingAddress);

  // Normalized first: a payload filled in under the old business role names still has its contacts, and
  // they belong in the boxes those roles are called by now.
  const storedRoles = normalizeRoles(d.roles);
  ALL_ROLE_KEYS.forEach((k) => fillRole(payload.roles[k], storedRoles[k]));

  payload.relatedEntities = (d.relatedEntities || []).map(makeEntity);
}

// `name` is sent alongside the two parts, not instead of them: it is the pair already joined, so every
// reader of "the contact's name" — the review summary, the staff panel, the Person that gets minted —
// has one field to read. A pre-split contact nobody has retouched keeps whatever it arrived with. The
// prefix is NOT folded in: a title is not part of the name a Person is filed under.
const outRole = (r) => {
  const joined = [r.firstName, r.lastName].map((v) => s(v).trim()).filter(Boolean).join(" ");
  return {
    prefix: s(r.prefix),
    firstName: s(r.firstName),
    lastName: s(r.lastName),
    name: joined || s(r.name),
    email: s(r.email),
    phone: s(r.phone)
  };
};

/**
 * The client's name as one string: the two boxes joined for an individual, the single box otherwise.
 * Mirrors RemsFormPayloadV1.EffectiveClientName, which is what the server files them under.
 */
export function intakeClientName (payload) {
  const joined = [payload.clientFirstName, payload.clientLastName]
    .map((v) => s(v).trim()).filter(Boolean).join(" ");
  return joined || s(payload.clientName);
}

/** Build the outgoing wire payload (dates: "" → null so DateOnly binds; addresses converted). */
export function buildIntakePayload (payload, industryGroup) {
  const key = intakeRoleSetKey(industryGroup);
  // The roles this client is ASKED, plus any they have already answered under a role the form has since
  // retired — dropping those on the next save would delete an answer the client gave us.
  const asked = GROUP_ROLES[key] || ALL_ROLE_KEYS;
  const answeredElsewhere = ALL_ROLE_KEYS.filter((k) => !asked.includes(k) && roleAny(payload.roles[k]));
  const roles = {};
  [...asked, ...answeredElsewhere].forEach((k) => { roles[k] = outRole(payload.roles[k]); });

  return {
    version: 1,
    clientName: intakeClientName(payload),
    clientPrefix: s(payload.clientPrefix),
    clientFirstName: s(payload.clientFirstName),
    clientLastName: s(payload.clientLastName),
    email: s(payload.email),
    mobileNumber: s(payload.mobileNumber),
    referralSource: s(payload.referralSource),
    referralSourceDetail: s(payload.referralSourceDetail),
    physicalAddress: fromAddress(payload.physicalAddress),
    mailingAddress: fromAddress(payload.mailingAddress),
    billingContactName: s(payload.billingContactName),
    billingEmail: s(payload.billingEmail),
    billingAddress: fromAddress(payload.billingAddress),
    spouseName: s(payload.spouseName),
    spousePhone: s(payload.spousePhone),
    spouseEmail: s(payload.spouseEmail),
    ein: s(payload.ein),
    contractStartDate: dateOrNull(payload.contractStartDate),
    contractEndDate: dateOrNull(payload.contractEndDate),
    originalTerm: s(payload.originalTerm),
    renewalTerms: s(payload.renewalTerms),
    poStartDate: dateOrNull(payload.poStartDate),
    poEndDate: dateOrNull(payload.poEndDate),
    roles,
    relatedEntities: payload.relatedEntities.map((e, i) => ({
      sourceKey: e.sourceKey || `related-${i + 1}`,
      fullName: s(e.fullName),
      emailAddress: s(e.emailAddress),
      phoneNumber: s(e.phoneNumber)
    }))
  };
}

/** Which role set an entity type is asked. The business family shares one — see remsContactRoles. */
export function intakeRoleSetKey (industryGroup) {
  return groupKey(industryGroup, isBusinessIndustryGroup(industryGroup));
}

/**
 * Every role this entity type is asked, as [{ key, label, hint, required }]. The billing contact is among
 * them: it is asked with the billing ADDRESS rather than in the Contacts card, but it is still a role and
 * it is still validated.
 */
export const intakeRoleDefs = (industryGroup) => {
  const key = intakeRoleSetKey(industryGroup);
  return key ? roleDefsFor(key) : [];
};

/**
 * What still has to be filled in. Mirrors RemsFormPayloadValidator on the server; both hosts gate on it
 * — the client's Review button and the Admin's Save — so neither offers an action the API will refuse.
 */
export function intakeIssues (payload, industryGroup) {
  const out = [];
  const individual = industryGroup === "individual";

  if (individual) {
    if (!filled(payload.clientFirstName)) out.push("First name is required.");
    if (!filled(payload.clientLastName)) out.push("Last name is required.");
    // A name that is filled in but is not a name — digits, punctuation, "N/A" — fails the same gate the
    // missing one does, rather than being caught only by the server after the client presses Submit.
    pushIf(out, nameIssue(payload.clientFirstName, "First name"));
    pushIf(out, nameIssue(payload.clientLastName, "Last name"));
  } else if (!filled(payload.clientName)) {
    out.push("Client / entity name is required.");
  }

  const addressIssue = "needs country, state, city, address line 1 and zip code.";
  if (!addressComplete(payload.physicalAddress)) out.push(`Physical address ${addressIssue}`);
  // Both are required: there is no "same as" flag deciding whether a mailing address exists, only a copy
  // button that fills it in for you.
  if (!addressComplete(payload.mailingAddress)) out.push(`Mailing address ${addressIssue}`);
  if (filled(payload.billingEmail) && !emailOk(payload.billingEmail)) {
    out.push("Billing email is not a valid email address.");
  }

  if (isBusinessIndustryGroup(industryGroup) && !filled(payload.ein)) {
    out.push("EIN is required for a business.");
  }

  // Driven off the same role definitions the cards are rendered from — all of them, wherever on the form
  // they are asked, so moving the billing contact up to the billing address cannot quietly stop it being
  // required.
  intakeRoleDefs(industryGroup).forEach(({ key, label, required }) => {
    const role = payload.roles[key];
    if (required) {
      if (!roleComplete(role)) out.push(`${label} needs a first name, a last name and a valid email.`);
    } else if (roleAny(role) && !roleComplete(role)) {
      out.push(`${label} is partly filled — complete the name and email, or clear it.`);
    }
    // Whatever HAS been typed into the two name boxes has to be a name, required contact or not.
    pushIf(out, nameIssue(role?.firstName, `${label} first name`));
    pushIf(out, nameIssue(role?.lastName, `${label} last name`));
  });

  // Name and email both required — the phone stays optional, as on every contact on this form.
  payload.relatedEntities.forEach((e, i) => {
    if (!filled(e.fullName)) out.push(`Entity #${i + 1} needs a client / entity name.`);
    if (!filled(e.emailAddress)) {
      out.push(`Entity #${i + 1} needs an email address.`);
    } else if (!emailOk(e.emailAddress)) {
      out.push(`Entity #${i + 1} has an invalid email address.`);
    }
  });

  return out;
}

/** Whether a related-entity row carries anything — what decides if clearing them needs confirming. */
export const relatedEntityHasData = (e) =>
  filled(e?.fullName) || filled(e?.emailAddress) || filled(e?.phoneNumber);

/**
 * A validation failure from either intake endpoint, split into what the FIELDS need and what the banner
 * shows: `{ fields, summary }`. The server sends one string of "path: message" pieces separated by
 * semicolons, and the paths are the payload's own — which is exactly what ClientIntakeFields looks its
 * per-field messages up by.
 */
export function parseIntakeFieldErrors (err) {
  const details = err?.response?.data?.error?.details || "";
  const fields = {};
  const summary = [];
  details.split(";").forEach((chunk) => {
    const piece = chunk.trim();
    if (!piece) return;
    const idx = piece.indexOf(":");
    if (idx === -1) { summary.push(piece); return; }
    fields[piece.slice(0, idx).trim()] = piece.slice(idx + 1).trim();
    summary.push(piece.slice(idx + 1).trim());
  });
  return { fields, summary: summary.length ? summary : ["One or more fields need your attention."] };
}
