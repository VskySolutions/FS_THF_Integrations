import { useTenantStore } from "stores/tenant";

// Renders UTC timestamps in the active tenant's time zone. The whole app stores
// and transmits UTC; display conversion happens only here.
//
//   const { formatDateTime, tenantTimeZone } = useDateFormat();
//   formatDateTime(row.updatedOnUtc) // -> "2026-06-09 11:42" in the tenant's tz
export function useDateFormat () {
  const tenantStore = useTenantStore();

  const tenantTimeZone = () => tenantStore.activeTenant?.timeZoneId || "UTC";

  // Parse a backend value as a UTC instant (assume UTC when no tz designator).
  const toUtcDate = (value) => {
    if (!value) return null;
    if (value instanceof Date) return value;
    let s = String(value);
    if (!/[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)) {
      s += "Z";
    }
    const d = new Date(s);
    return Number.isNaN(d.getTime()) ? null : d;
  };

  const partsFor = (date, options) => {
    const fmt = new Intl.DateTimeFormat("en-GB", { timeZone: tenantTimeZone(), hour12: false, ...options });
    return Object.fromEntries(fmt.formatToParts(date).map((p) => [p.type, p.value]));
  };

  const formatDateTime = (value, placeholder = "—") => {
    const d = toUtcDate(value);
    if (!d) return placeholder;
    const p = partsFor(d, { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" });
    return `${p.year}-${p.month}-${p.day} ${p.hour}:${p.minute}`;
  };

  const formatDate = (value, placeholder = "—") => {
    const d = toUtcDate(value);
    if (!d) return placeholder;
    const p = partsFor(d, { year: "numeric", month: "2-digit", day: "2-digit" });
    return `${p.year}-${p.month}-${p.day}`;
  };

  return { formatDateTime, formatDate, tenantTimeZone };
}
