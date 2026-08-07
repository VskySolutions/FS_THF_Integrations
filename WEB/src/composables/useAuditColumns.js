import { useDateFormat } from "composables/useDateFormat";

// The four audit columns every list carries: who created a record and when, who last touched it and when.
// Hidden by default — they are reference detail, not something a list should lead with — but always
// present in the Columns menu so the trail is one click away on any page rather than only on the detail
// screen of whichever entities happened to expose it.
//
//   const columns = computed(() => [ ...pageColumns, ...auditColumns() ]);
//
// `overrides` remaps the row keys for a list whose API names them differently (e.g. SMTP accounts return
// `createdByName`), so a page never has to hand-roll the whole set to fix one field:
//
//   auditColumns({ createdBy: "createdByName" })
//
// Pass `only` to emit a subset — for lists that already define one of these themselves and would
// otherwise end up with two columns of the same name, which AppDataTable's visibility map cannot tell
// apart. Prefer deleting the page's own definition and taking all four from here.
export function useAuditColumns () {
  const fmt = useDateFormat();

  const dateCell = (key) => (row) => (row?.[key] ? fmt.formatDateTime(row[key]) : "—");
  const textCell = (key) => (row) => row?.[key] || "—";

  return function auditColumns ({ overrides = {}, only = null } = {}) {
    const key = (name) => overrides[name] || name;

    const all = [
      { name: "createdBy", label: "Created By", field: textCell(key("createdBy")), align: "left", sortable: true, default: false, filterable: false },
      { name: "createdOnUtc", label: "Created On", field: dateCell(key("createdOnUtc")), align: "left", sortable: true, default: false, filterable: false },
      { name: "updatedBy", label: "Updated By", field: textCell(key("updatedBy")), align: "left", sortable: true, default: false, filterable: false },
      { name: "updatedOnUtc", label: "Updated On", field: dateCell(key("updatedOnUtc")), align: "left", sortable: true, default: false, filterable: false }
    ];

    return only ? all.filter((c) => only.includes(c.name)) : all;
  };
}
