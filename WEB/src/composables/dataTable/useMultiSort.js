import { ref, computed, watch } from "vue";

export default function useMultiSort ({
  lsSorts = {},
  saveDataTableState,
  onApplySort
}) {
  const multiSort = ref(
    lsSorts && Object.keys(lsSorts).length
      ? Object.entries(lsSorts).map(([column, direction]) => ({
        column,
        direction
      }))
      : [{ column: "", direction: "" }]
  );

  const addSortLevel = () => {
    multiSort.value.push({
      column: "",
      direction: ""
    });
  };

  const removeSortLevel = (row) => {
    const index = multiSort.value.findIndex(r => r === row);

    if (index !== -1) {
      multiSort.value.splice(index, 1);
    }

    if (multiSort.value.length === 0) {
      multiSort.value.push({
        column: "",
        direction: ""
      });
    }
  };

  const buildSortObject = () => {
    return Object.fromEntries(
      multiSort.value
        .filter(sort => sort.column && sort.direction)
        .map(sort => [sort.column, sort.direction])
    );
  };

  const applyMultiSort = () => {
    const sorts = buildSortObject();

    saveDataTableState({
      sorts
    });

    if (onApplySort) {
      onApplySort(sorts);
    }
  };

  watch(
    multiSort,
    () => {
      const sorts = buildSortObject();

      saveDataTableState({
        sorts
      });
    },
    {
      deep: true
    }
  );

  const selectedSortCount = computed(() => {
    return multiSort.value.filter(sort => sort.column).length;
  });

  return {
    multiSort,
    addSortLevel,
    removeSortLevel,
    applyMultiSort,
    selectedSortCount
  };
}
