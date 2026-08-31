import { useDateFormat } from "composables/useDateFormat";

// The four audit columns every list carries: who created a record and when, who last touched it and when.
//
// The convention, one place so every list keeps it:
//
//   * Updated By and Updated On are SHOWN by default, and are the last two columns before Actions.
//     "When was this last touched, and by whom" is what a reader scanning a list actually wants, and
//     every list should answer it the same way and in the same place.
//   * Created By and Created On are NOT shown by default. They are reference detail — the same fact
//     frozen at the beginning — and are one click away in the Columns menu on any page.
//   * Lists sort by Updated On, newest first (AppDataTable's default, which pages inherit by simply not
//     overriding it).
//
//   const columns = computed(() => [
//     ...pageColumns,
//     ...auditColumns(),
//     { name: "actions", label: "Actions", field: "actions", align: "right" }
//   ]);
//
// `overrides` remaps the row keys for a list whose API names them differently (e.g. SMTP accounts return
// `createdByName`), so a page never has to hand-roll the whole set to fix one field:
//
//   auditColumns({ overrides: { createdBy: "createdByName" } })
//
// Pass `only` to emit a subset — for lists that already define one of these themselves and would
// otherwise end up with two columns of the same name, which AppDataTable's visibility map cannot tell
// apart. Prefer deleting the page's own definition and taking all four from here: a page that defines
// its own is a page that has quietly opted out of the convention above.
export function useAuditColumns () {
  const fmt = useDateFormat();

  const dateCell = (key) => (row) => (row?.[key] ? fmt.formatDateTime(row[key]) : "—");
  const textCell = (key) => (row) => row?.[key] || "—";
  // What the column ORDERS by where a table sorts its own rows. The cell reads "MM/DD/YYYY hh:mm AM",
  // which as text sorts by month before year; the raw timestamp is an ISO instant and sorts as it stands.
  const dateSort = (key) => (row) => row?.[key] || "";

  return function auditColumns ({ overrides = {}, only = null } = {}) {
    const key = (name) => overrides[name] || name;

    // Created By / Updated By are NOT sortable. They are stored as user ids and turned into names one
    // page at a time, so there is no column for the database to order the whole set by — and sorting the
    // twenty names already fetched would order the page rather than the list. Better an unclickable
    // heading than one that appears to do something and does something else.
    const all = [
      { name: "createdBy", label: "Created By", field: textCell(key("createdBy")), align: "left", sortable: false, default: false, filterable: false },
      { name: "createdOnUtc", label: "Created On", field: dateCell(key("createdOnUtc")), sort: dateSort(key("createdOnUtc")), align: "left", sortable: true, default: false, filterable: false },
      { name: "updatedBy", label: "Updated By", field: textCell(key("updatedBy")), align: "left", sortable: false, default: true, filterable: false },
      { name: "updatedOnUtc", label: "Updated On", field: dateCell(key("updatedOnUtc")), sort: dateSort(key("updatedOnUtc")), align: "left", sortable: true, default: true, filterable: false }
    ];

    return only ? all.filter((c) => only.includes(c.name)) : all;
  };
}
