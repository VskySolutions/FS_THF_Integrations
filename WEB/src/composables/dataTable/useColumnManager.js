import { computed, watch } from "vue";

export function useColumnManager ({
  columns,
  selectedColumnNames,
  saveColumnsState,
  isResizing
}) {
  const allColumns = computed(() =>
    columns.value.map(col => col.name)
  );

  const defaultColumnNames = computed(() =>
    columns.value
      .filter(col => col.default === true)
      .map(col => col.name)
  );

  watch(
    () => columns.value,
    () => {
      if (
        !Array.isArray(selectedColumnNames.value) ||
        selectedColumnNames.value.length === 0
      ) {
        selectedColumnNames.value = [...defaultColumnNames.value];
      }
    },
    {
      immediate: true,
      deep: true
    }
  );

  const selectAllColumns = () => {
    selectedColumnNames.value = [...allColumns.value];
  };

  const defaultColumns = () => {
    selectedColumnNames.value = [...defaultColumnNames.value];
  };

  const toggleColumn = (colValue) => {
    const index = selectedColumnNames.value.indexOf(colValue);

    if (index > -1) {
      selectedColumnNames.value.splice(index, 1);
    } else {
      selectedColumnNames.value.push(colValue);
    }
  };

  const allColumnNames = computed(() =>
    columns.value
      .filter(col => col.name !== "actions")
      .map(col => ({
        label: col.label,
        value: col.name
      }))
  );

  const computedColumns = computed(() => {
    const selected = Array.isArray(selectedColumnNames.value)
      ? [...selectedColumnNames.value]
      : [];

    const visibleColumns =
      selected.length > 0
        ? selected
        : defaultColumnNames.value;

    return columns.value
      .filter(col => visibleColumns.includes(col.name))
      .map(col => ({
        ...col,
        sortable: !isResizing.value && col.sortable
      }));
  });

  watch(
    () => [...selectedColumnNames.value],
    (val) => {
      saveColumnsState(val);
    },
    {
      deep: true
    }
  );

  return {
    allColumns,
    defaultColumnNames,
    selectedColumnNames,
    selectAllColumns,
    defaultColumns,
    toggleColumn,
    allColumnNames,
    computedColumns
  };
}
