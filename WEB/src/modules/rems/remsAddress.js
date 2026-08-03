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
  postalCode: wire?.zip ?? ""
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
  countryName: str(a?.countryName)
});

// The server reports validation failures against the wire names ("physicalAddress.street"); the field-set
// looks them up by the canonical ones. This re-keys one address's messages for it.
const ERROR_KEYS = {
  street: "addressLine1",
  addressLine2: "addressLine2",
  city: "cityName",
  state: "stateName",
  zip: "postalCode",
  countryCode: "countryCode"
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

/** A complete address: everything except line 2. */
export const addressComplete = (a) =>
  [a?.countryCode, a?.addressLine1, a?.cityName, a?.stateName, a?.postalCode].every(present);

/** True when a wire-shaped address carries content (country excluded, as above). */
export const hasAddress = (wire) => [wire?.street, wire?.addressLine2, wire?.city, wire?.state, wire?.zip].some(present);

/** One-line rendering of a wire-shaped address, used by every read-only REMS view. */
export const addressText = (wire) => {
  if (!hasAddress(wire)) return "—";
  const cityLine = [wire.city, wire.state, wire.zip].filter(present).join(" ");
  return [wire.street, wire.addressLine2, cityLine, wire.countryName || wire.countryCode].filter(present).join(", ");
};
