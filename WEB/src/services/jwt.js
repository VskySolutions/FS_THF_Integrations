// Minimal client-side JWT helpers. The token is NOT verified here — it is only decoded
// to read the claims the server already issued (permissions drive permission-gated UI).

export function decodeJwtPayload (token) {
  if (!token || typeof token !== "string") return null;
  const parts = token.split(".");
  if (parts.length < 2) return null;
  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const json = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join("")
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

// The effective permission keys carried by the active-tenant-scoped token. .NET emits a
// repeated "permission" claim, which serializes to a string (one) or an array (many).
export function decodeJwtPermissions (token) {
  const payload = decodeJwtPayload(token);
  const perms = payload?.permission;
  if (Array.isArray(perms)) return perms;
  if (typeof perms === "string") return [perms];
  return [];
}
