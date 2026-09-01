import { DEFAULT_COUNTRY_ISO, countryNameFromIso } from "composables/useCountries";

// REMS addresses travel in a legacy wire shape — { street, addressLine2, city, state, stateCode, zip,
// countryCode, countryName } — shared by the public form payload (RemsAddressPayload) and the engagement
// workspace DTOs (RemsAddressInput / RemsAddressView). Those names are FROZEN: the payload is persisted
// verbatim in REMSFormDraft.DraftPayload and the immutable REMSFormSubmission.SubmittedPayload, so
// renaming them would blank out every form submitted before this change.
//
// AppAddressFields binds the canonical model instead, so REMS screens convert at their own boundary:
// toAddress() when a record loads, fromAddress() on the way out. This module is the only place that
// knows both shapes.

const present = (v) => !!String(v ?? "").trim();
const str = (v) => (v == null ? "" : String(v));

/**
 * Wire → canonical. Records saved before the country cascade carry no codes, hence the default country.
 * countryName is resolved from the code rather than trusted, so the value here already matches what
 * AppAddressFields would derive — otherwise every load would look like an unsaved edit.
 */
export const toAddress = (wire) => ({
  countryCode: wire?.countryCode || DEFAULT_COUNTRY_ISO,
  countryName: countryNameFromIso(wire?.countryCode || DEFAULT_COUNTRY_ISO),
  stateCode: wire?.stateCode || null,
  stateName: wire?.state ?? "",
  cityName: wire?.city ?? "",
  addressLine1: wire?.street ?? "",
  addressLine2: wire?.addressLine2 ?? "",
  postalCode: wire?.zip ?? "",
  // Who the post is addressed to. On every address in the shape, asked for on the ones whose form opts
  // in — today the client intake's billing addresses, where "where does the invoice go?" and "who is it
  // addressed to?" are one question. These five keep their canonical names on both sides of the wire;
  // only the postal lines carry the frozen legacy names above.
  suffix: wire?.suffix ?? "",
  firstName: wire?.firstName ?? "",
  lastName: wire?.lastName ?? "",
  email: wire?.email ?? "",
  phone: wire?.phone ?? ""
});

/** A blank canonical address, with the app-wide default country pre-selected. */
export const blankAddress = () => toAddress(null);

/** Canonical → wire. */
export const fromAddress = (a) => ({
  street: str(a?.addressLine1),
  addressLine2: str(a?.addressLine2),
  city: str(a?.cityName),
  state: str(a?.stateName),
  stateCode: str(a?.stateCode),
  zip: str(a?.postalCode),
  countryCode: str(a?.countryCode),
  countryName: str(a?.countryName),
  suffix: str(a?.suffix),
  firstName: str(a?.firstName),
  lastName: str(a?.lastName),
  email: str(a?.email),
  phone: str(a?.phone)
});

/**
 * The postal half of a canonical address, copied into an existing object. What "Copy from physical"
 * actually does: it moves the PLACE and leaves whoever this address is addressed to alone, because a
 * billing address copied from the office is still addressed to the person in accounts payable.
 *
 * Assigned into the target rather than replacing it, so the bound model instance survives — the
 * field-set resolves its country → state → city cascade from that instance — and so the row's own
 * local key survives with it.
 */
const POSTAL_KEYS = [
  "countryCode", "countryName", "stateCode", "stateName", "cityName",
  "addressLine1", "addressLine2", "postalCode"
];

export const copyPostalInto = (target, source) => {
  POSTAL_KEYS.forEach((k) => { target[k] = source?.[k] ?? ""; });
};

// The server reports validation failures against the wire names ("physicalAddress.street"); the field-set
// looks them up by the canonical ones. This re-keys one address's messages for it.
const ERROR_KEYS = {
  street: "addressLine1",
  addressLine2: "addressLine2",
  city: "cityName",
  state: "stateName",
  zip: "postalCode",
  countryCode: "countryCode",
  // The addressee's own fields are named the same on both sides, so these map to themselves. Listed
  // rather than assumed, because this table is also what says which keys a message can arrive under.
  firstName: "firstName",
  lastName: "lastName",
  email: "email",
  phone: "phone"
};

/** Server messages for one address, re-keyed from wire names to the canonical field names. */
export const addressErrors = (errors, prefix) => {
  const out = {};
  Object.entries(ERROR_KEYS).forEach(([wire, canonical]) => {
    const message = errors?.[`${prefix}.${wire}`];
    if (message) out[canonical] = message;
  });
  return out;
};

/**
 * Did anyone actually enter an address? The country is deliberately excluded — every blank address
 * starts with one pre-selected, so on its own it must not count as content.
 */
export const addressHasAny = (a) => [a?.addressLine1, a?.addressLine2, a?.cityName, a?.stateName, a?.postalCode].some(present);

/** Anything said about the addressee. Neither the suffix nor the place counts — see addressHasAny. */
const addressHasContact = (a) => [a?.firstName, a?.lastName, a?.email, a?.phone].some(present);

/**
 * A place, an addressee, or both — what decides whether a billing row is somebody's answer or a block
 * they opened and thought better of. Either half alone is a real answer: an invoice can go to a street
 * with no name on it, or to a named person by email with no street at all.
 */
export const addressHasAnyContent = (a) => addressHasAny(a) || addressHasContact(a);

/**
 * The addressee as they are addressed — the two name parts joined, with the particle in FRONT, the order
 * the form asks them in. Serves the canonical and the wire shape alike: those five field names are the
 * same on both sides.
 */
/**
 * The same two halves, unjoined, for a surface that RENDERS the addressee rather than needing a string —
 * AppNameWithSuffix draws the particle in bold, and cannot find it inside a joined name.
 */
export const addresseeParts = (a) => {
  const name = [a?.firstName, a?.lastName].map((v) => String(v ?? "").trim()).filter(Boolean).join(" ");
  return { name, suffix: name ? String(a?.suffix ?? "").trim() : "" };
};

export const addresseeName = (a) => {
  const name = [a?.firstName, a?.lastName].map((v) => String(v ?? "").trim()).filter(Boolean).join(" ");
  if (!name) return "";
  const suffix = String(a?.suffix ?? "").trim();
  return suffix ? `${suffix} ${name}` : name;
};

/** A complete address: everything except line 2. */
export const addressComplete = (a) =>
  [a?.countryCode, a?.addressLine1, a?.cityName, a?.stateName, a?.postalCode].every(present);

/** True when a wire-shaped address carries content (country excluded, as above). */
const hasAddress = (wire) => [wire?.street, wire?.addressLine2, wire?.city, wire?.state, wire?.zip].some(present);

/** A place, an addressee, or both, on a WIRE-shaped address — see addressHasAnyContent. */
const wireHasAnyContent = (wire) =>
  hasAddress(wire) || [wire?.firstName, wire?.lastName, wire?.email, wire?.phone].some(present);

/**
 * A payload's billing addresses, with the single `billingAddress` that a payload written before the list
 * existed carries folded in as the first row. Mirrors RemsFormPayloadV1.EffectiveBillingAddresses on the
 * server, so both sides read one submission the same way — a payload holding BOTH keeps the list, which
 * was written later by a form that offered the single box nowhere.
 */
export const billingAddressList = (payload) => {
  const list = (payload?.billingAddresses || []).filter(wireHasAnyContent);
  if (list.length) return list;
  return wireHasAnyContent(payload?.billingAddress) ? [payload.billingAddress] : [];
};

/** One-line rendering of a wire-shaped address, used by every read-only REMS view. */
export const addressText = (wire) => {
  if (!hasAddress(wire)) return "—";
  const cityLine = [wire.city, wire.state, wire.zip].filter(present).join(" ");
  return [wire.street, wire.addressLine2, cityLine, wire.countryName || wire.countryCode].filter(present).join(", ");
};
