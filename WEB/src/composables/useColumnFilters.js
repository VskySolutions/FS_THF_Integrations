import { reactive, computed } from "vue";

// Generic per-column filtering for AppDataTable lists. Builds one reactive filter value per
// filterable column and a `filteredRows` view (client-side, over the currently loaded page).
//
// Column opt-outs / hints (on the column definition):
//   filterable: false            → no filter control for this column
//   filterOptions: [{label,value}] → renders a select (matched by equality) instead of a text box
// Anything else gets a case-insensitive "contains" text filter over the column's displayed value.
//
//   const { filters, filterableColumns, filteredRows, filterChips, removeFilter, clearFilters }
//     = useColumnFilters(columns, rows);
export function useColumnFilters (columnsInput, rows) {
  const colList = () => (Array.isArray(columnsInput) ? columnsInput : (columnsInput.value || []));

  const filterableColumns = computed(() =>
    colList().filter((c) => c.name !== "actions" && c.filterable !== false));

  const filters = reactive({});

  // The value a column shows for a row (honours function `field` accessors, e.g. formatted dates).
  const valueOf = (col, row) => {
    const f = col.field;
    return typeof f === "function" ? f(row) : row[f ?? col.name];
  };

  const hasValue = (v) => v !== null && v !== undefined && v !== "";

  const filteredRows = computed(() => {
    let result = rows.value || [];
    for (const col of filterableColumns.value) {
      const fv = filters[col.name];
      if (!hasValue(fv)) continue;
      if (col.filterOptions) {
        result = result.filter((r) => valueOf(col, r) === fv);
      } else {
        const needle = String(fv).toLowerCase();
        result = result.filter((r) => {
          const v = valueOf(col, r);
          return v != null && String(v).toLowerCase().includes(needle);
        });
      }
    }
    return result;
  });

  const chipLabel = (col) => {
    const fv = filters[col.name];
    if (col.filterOptions) {
      const opt = col.filterOptions.find((o) => o.value === fv);
      return `${col.label}: ${opt ? opt.label : fv}`;
    }
    return `${col.label}: ${fv}`;
  };

  const filterChips = computed(() =>
    filterableColumns.value
      .filter((c) => hasValue(filters[c.name]))
      .map((c) => ({ key: c.name, label: chipLabel(c) })));

  const removeFilter = (key) => { filters[key] = null; };
  const clearFilters = () => { filterableColumns.value.forEach((c) => { filters[c.name] = null; }); };

  return { filters, filterableColumns, filteredRows, filterChips, removeFilter, clearFilters };
}
