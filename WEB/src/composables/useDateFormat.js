import { useTenantStore } from "stores/tenant";

// Renders UTC timestamps in the active tenant's time zone. The whole app stores
// and transmits UTC; display conversion happens only here.
//
//   const { formatDateTime, tenantTimeZone } = useDateFormat();
//   formatDateTime(row.updatedOnUtc) // -> "06-09-2026 11:42 AM" in the tenant's tz
//
// App-wide display format: MM-DD-YYYY with a 12-hour (AM/PM) clock.
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

  // en-US so the day-period renders as AM/PM; explicit 2-digit options keep numbers locale-stable.
  const partsFor = (date, options) => {
    const fmt = new Intl.DateTimeFormat("en-US", { timeZone: tenantTimeZone(), ...options });
    return Object.fromEntries(fmt.formatToParts(date).map((p) => [p.type, p.value]));
  };

  const formatDateTime = (value, placeholder = "—") => {
    const d = toUtcDate(value);
    if (!d) return placeholder;
    const p = partsFor(d, { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hour12: true });
    return `${p.month}-${p.day}-${p.year} ${p.hour}:${p.minute} ${p.dayPeriod}`;
  };

  const formatDate = (value, placeholder = "—") => {
    const d = toUtcDate(value);
    if (!d) return placeholder;
    const p = partsFor(d, { year: "numeric", month: "2-digit", day: "2-digit" });
    return `${p.month}-${p.day}-${p.year}`;
  };

  return { formatDateTime, formatDate, tenantTimeZone };
}
