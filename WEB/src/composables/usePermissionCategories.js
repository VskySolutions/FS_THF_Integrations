// Shared permission-key categorisation + humanisation (WO-70). Centralised so the Permission Group
// form, detail page and role panel group/label permission keys identically.
//
// Category derivation (by key prefix):
//   jobs.*       → Jobs (except jobs.schedule → Schedules)
//   tenants.*    → Tenants
//   users.* / persons.* → Users
//   mappings.*   → Mappings
//   customers.*  → Customers
//   roles.* / groups.*  → Access
//   logs.* / health.*   → System
//   (anything else)     → Other

// Display order for the rendered category sections.
export const CATEGORY_ORDER = Object.freeze([
  "Tenants", "Users", "Access", "Customers", "Mappings", "Jobs", "Schedules", "System", "Other"
]);

// Super-admin-only / elevated keys that a Tenant Admin cannot typically grant. Used as a
// best-effort ceiling hint in the form (the backend enforces the real ceiling). Keep conservative.
export const ELEVATED_KEYS = Object.freeze(["tenants.archive", "roles.assign", "tenants.write"]);

export function categoryForKey (key) {
  if (!key) return "Other";
  if (key === "jobs.schedule") return "Schedules";
  const prefix = key.split(".")[0];
  switch (prefix) {
    case "jobs": return "Jobs";
    case "tenants": return "Tenants";
    case "users":
    case "persons": return "Users";
    case "mappings": return "Mappings";
    case "customers": return "Customers";
    case "roles":
    case "groups": return "Access";
    case "logs":
    case "health": return "System";
    default: return "Other";
  }
}

// "jobs.schedule" → "Jobs · Schedule"; underscores become spaces (title-cased segments).
export function humanizeKey (key) {
  if (!key) return "";
  return key
    .split(".")
    .map((seg) => seg.split("_").map((w) => w.charAt(0).toUpperCase() + w.slice(1)).join(" "))
    .join(" · ");
}

// Group a flat list of keys into ordered category buckets: [{ category, keys[] }].
export function groupKeysByCategory (keys = []) {
  const buckets = new Map();
  for (const key of keys) {
    const cat = categoryForKey(key);
    if (!buckets.has(cat)) buckets.set(cat, []);
    buckets.get(cat).push(key);
  }
  const ordered = CATEGORY_ORDER.filter((c) => buckets.has(c));
  // Any unexpected categories (defensive) appended after the known order.
  for (const cat of buckets.keys()) {
    if (!ordered.includes(cat)) ordered.push(cat);
  }
  return ordered.map((category) => ({ category, keys: buckets.get(category).slice().sort() }));
}

export function usePermissionCategories () {
  return { CATEGORY_ORDER, ELEVATED_KEYS, categoryForKey, humanizeKey, groupKeysByCategory };
}
