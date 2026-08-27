import { useTenantStore } from "stores/tenant";

/**
 * A calendar date — a DateOnly "YYYY-MM-DD" — as MM/DD/YYYY.
 *
 * Deliberately NOT part of the composable below and deliberately not routed through a time zone. A
 * DateOnly is a date, not an instant: a fiscal year end of 31 December is the 31st of December wherever
 * it is read. Converting one is how it becomes the 30th for everybody west of Greenwich — which is also
 * why this reformats the STRING rather than going near a Date object, since `new Date("2026-12-31")`
 * parses as UTC midnight and reads back a day early in the Americas.
 *
 * Being a plain function rather than a composable member matters too: it needs no tenant store, so the
 * anonymous client-facing screens can use it.
 *
 * Anything that is not a recognisable ISO date is passed through untouched — a value this cannot read is
 * better shown as it stands than swallowed.
 */
export function formatDateOnly (value, placeholder = "—") {
  if (!value) return placeholder;
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value));
  return m ? `${m[2]}/${m[3]}/${m[1]}` : String(value);
}

// Renders UTC timestamps in the active tenant's time zone. The whole app stores
// and transmits UTC; display conversion happens only here.
//
//   const { formatDateTime, tenantTimeZone } = useDateFormat();
//   formatDateTime(row.updatedOnUtc) // -> "06/24/2025 01:01 AM" in the tenant's tz
//
// THE app-wide display format, for every date the application shows: MM/DD/YYYY, with a 12-hour (AM/PM)
// clock on anything carrying a time. Calendar dates read the same way through formatDateOnly above — the
// separator does not change between a date and a timestamp, or between one screen and another.
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
    return `${p.month}/${p.day}/${p.year} ${p.hour}:${p.minute} ${p.dayPeriod}`;
  };

  const formatDate = (value, placeholder = "—") => {
    const d = toUtcDate(value);
    if (!d) return placeholder;
    const p = partsFor(d, { year: "numeric", month: "2-digit", day: "2-digit" });
    return `${p.month}/${p.day}/${p.year}`;
  };

  // How far the tenant's clock runs ahead of UTC at a given instant (DST-aware, since the offset is
  // read at that instant rather than assumed constant).
  const tzOffsetMs = (utcMs) => {
    const p = partsFor(new Date(utcMs), {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false
    });
    // Intl renders midnight as "24" in some locales' hour-cycle; normalise it back to 0.
    return Date.UTC(+p.year, +p.month - 1, +p.day, +p.hour % 24, +p.minute, +p.second) - utcMs;
  };

  // The inverse of the formatters above: the UTC instant at which the tenant's clock reads the start
  // (or end) of the given yyyy-mm-dd. Date-only filters need this — a range picked as tenant-local days
  // and compared against UTC timestamps otherwise drops or admits rows around each boundary, and a
  // "to" date taken at face value would exclude everything created during that very day.
  const zonedDayBoundaryUtc = (isoDate, edge = "start") => {
    if (!isoDate) return undefined;
    const [y, m, d] = String(isoDate).split("-").map(Number);
    if (!y || !m || !d) return undefined;
    const wall = edge === "end"
      ? Date.UTC(y, m - 1, d, 23, 59, 59, 999)
      : Date.UTC(y, m - 1, d, 0, 0, 0, 0);
    // One correction pass: read the offset at the naive instant, then shift by it. Only a boundary
    // falling inside a DST transition could land an hour out, which no day filter can express anyway.
    return new Date(wall - tzOffsetMs(wall)).toISOString();
  };

  return { formatDateTime, formatDate, tenantTimeZone, zonedDayBoundaryUtc };
}
