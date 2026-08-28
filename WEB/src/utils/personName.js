// What a person's first or last name may be, in one place, so every form that asks for one asks for the
// same thing — and so the browser and the server agree (the C# twin is Api/Validators/PersonNames.cs).
//
// A name is letters. What is NOT a name, and is what this exists to keep out, is a phone number typed
// into the surname box, an email address, "N/A", "test123", and the punctuation a keyboard slip leaves
// behind. Those reach us often enough to matter: a contact becomes a Person record, and a Person filed
// under "asdf1" is a client nobody can find again.
//
// Three marks are allowed inside a name because real names carry them — the hyphen (Smith-Jones), the
// apostrophe (O'Brien, D'Angelo) and the period (St. John) — and so is the internal space, which is not
// a lapse in strictness but the reason the name is asked for in two boxes at all: "Van Der Berg" is one
// surname, and a rule that rejected it would send this client's record back to the guesswork the split
// was made to end. Leading, trailing and doubled spaces are still refused — those are typing, not names.

export const NAME_MAX_LENGTH = 100;

/**
 * The generational suffixes offered beside a name — Jr., Sr., II, III, IV.
 *
 * Suggestions, not a closed list: these are what most people need, not all a person may have, so every
 * field offering them stays free text. Here rather than in a REMS module because two unrelated places
 * ask for one now — the client's own name on the request, and each contact on the intake form.
 */
export const NAME_SUFFIXES = [
  { value: "Jr.", label: "Jr.", caption: "Junior" },
  { value: "Sr.", label: "Sr.", caption: "Senior" },
  { value: "II", label: "II", caption: "The second" },
  { value: "III", label: "III", caption: "The third" },
  { value: "IV", label: "IV", caption: "The fourth" }
];

/** Mirrors the nvarchar(16) the suffix columns are stored in. */
export const NAME_SUFFIX_MAX_LENGTH = 16;

// A letter from any script, plus the combining marks that go with one.
const LETTER = /^[\p{L}\p{M}]/u;
// The whole name: opens on a letter, and carries nothing but letters, single spaces and the three marks.
const NAME_SHAPE = /^[\p{L}\p{M}][\p{L}\p{M} '’.-]*$/u;

/**
 * What is wrong with a name, as a sentence to show under the field — or "" when there is nothing wrong.
 *
 * An EMPTY value is never this function's complaint: whether a name is required is the form's business
 * and differs field by field (an optional contact is blank until somebody starts filling it in). So an
 * empty string passes here and the required rule speaks for itself.
 */
export function nameIssue (value, label = "This name") {
  const raw = String(value ?? "");
  const trimmed = raw.trim();
  if (!trimmed) return "";

  if (raw !== trimmed) return `${label} cannot start or end with a space.`;
  if (trimmed.length > NAME_MAX_LENGTH) return `${label} is at most ${NAME_MAX_LENGTH} characters.`;
  if (/\d/.test(trimmed)) return `${label} cannot contain numbers.`;
  if (/\s{2,}/.test(trimmed)) return `${label} cannot contain two spaces in a row.`;
  if (!LETTER.test(trimmed)) return `${label} must start with a letter.`;
  if (!NAME_SHAPE.test(trimmed)) {
    return `${label} can only contain letters, spaces, hyphens, apostrophes and periods.`;
  }
  return "";
}

/** True when a name is usable as it stands (an empty one included — see nameIssue). */
export const nameIsValid = (value) => nameIssue(value) === "";

/**
 * Quasar `:rules` for a name field.
 *
 *   <app-text-field :rules="nameRules('First Name', { required: true })" />
 */
export function nameRules (label = "This name", { required = false } = {}) {
  const rules = [];
  if (required) {
    rules.push((v) => !!String(v ?? "").trim() || `${label} is required.`);
  }
  rules.push((v) => nameIssue(v, label) || true);
  return rules;
}
