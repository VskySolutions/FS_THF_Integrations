import { Cookies } from "quasar";

// Per-page preference persistence via browser cookies (UI Development Standards §4.2).
// Stores page size, sort state, column widths and active filters keyed by page.
//
// Usage:
//   const prefs = usePreferences("tenants");
//   prefs.set("pageSize", 50);
//   const size = prefs.get("pageSize", 20);
export function usePreferences (pageKey) {
  const cookieName = `pref:${pageKey}`;
  const cookieOptions = { expires: 365, path: "/", sameSite: "Lax" };

  const all = () => {
    const raw = Cookies.get(cookieName);
    return raw && typeof raw === "object" ? raw : {};
  };

  const writeAll = (prefs) => {
    Cookies.set(cookieName, prefs, cookieOptions);
  };

  const get = (key, fallback = null) => {
    const prefs = all();
    return Object.prototype.hasOwnProperty.call(prefs, key) ? prefs[key] : fallback;
  };

  const set = (key, value) => {
    const prefs = all();
    prefs[key] = value;
    writeAll(prefs);
  };

  const merge = (partial) => {
    writeAll({ ...all(), ...partial });
  };

  const remove = (key) => {
    const prefs = all();
    delete prefs[key];
    writeAll(prefs);
  };

  const clear = () => Cookies.remove(cookieName, { path: "/" });

  return { all, get, set, merge, remove, clear };
}
