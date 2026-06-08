import { ref } from "vue";

export function useMultiSelectDropdown (serviceCall, config = {}) {
  const list = ref([]);
  const listFilter = ref([]);
  const filterValue = ref("");

  const {
    labelKey = "name",
    valueKey = "id",
    colorKey = null,
    bgColorKey = null,
    labelFn = null,
    disableFn = null,
    afterLoad = null
  } = config;

  async function load (...params) {
    const resp = await serviceCall(...params);

    const mapped = resp
      .map(item => {
        const text = labelFn ? labelFn(item) : getNestedValue(item, labelKey);
        return {
          text,
          value: getNestedValue(item, valueKey),
          color: colorKey ? getNestedValue(item, colorKey) : null,
          bgColor: bgColorKey ? getNestedValue(item, bgColorKey) : null,
          disable: disableFn ? disableFn(item) : false
        };
      });

    list.value = mapped;
    listFilter.value = mapped.map(x => ({ ...x, disable: false }));

    if (afterLoad) {
      afterLoad(mapped);
    }
  }

  function filter (val, update) {
    filterValue.value = val || "";

    update(() => {
      const needle = filterValue.value.toLowerCase();

      list.value = !needle
        ? listFilter.value
        : listFilter.value.filter(v =>
          v.text?.toLowerCase().includes(needle)
        );
    });
  }

  function reset () {
    filterValue.value = "";
    list.value = listFilter.value;
  }

  function getValueByLabel (label) {
    if (!label) return null;

    const normalizedLabel = label.toLowerCase();

    const found = list.value.find(
      x => x.text?.toLowerCase() === normalizedLabel
    );

    return found ? found.value : null;
  }

  function getValuesByLabels (labels) {
    if (!Array.isArray(labels)) return [];
    const labelSet = new Set(labels.map(l => l.toLowerCase()));
    return list.value.filter(x => labelSet.has(x.text?.toLowerCase())).map(x => x.value);
  }

  return {
    list,
    load,
    filter,
    reset,
    getValueByLabel,
    getValuesByLabels
  };
}

export function useSingleSelectDropdown (serviceCall, config = {}) {
  const list = ref([]);
  const listFilter = ref([]);

  const {
    labelKey = "name",
    valueKey = "id",
    colorKey = null,
    bgColorKey = null,
    labelFn = null,
    dataKey = null
  } = config;

  async function load (...params) {
    const resp = await serviceCall(...params);

    const mapped = resp
      .map(item => {
        const text = labelFn
          ? labelFn(item)
          : getNestedValue(item, labelKey);

        return {
          text,
          value: getNestedValue(item, valueKey),
          data: dataKey ? getNestedValue(item, dataKey) : null,
          color: colorKey ? getNestedValue(item, colorKey) : null,
          bgColor: bgColorKey ? getNestedValue(item, bgColorKey) : null,
        };
      });

    list.value = mapped;
    listFilter.value = mapped.map(x => ({ ...x, disable: false }));
    return mapped; // important for default selection
  }

  function filter (val, update) {
    update(() => {
      const needle = val ? val.toLowerCase() : "";

      if (!needle) {
        list.value = listFilter.value;
      } else {
        list.value = listFilter.value.filter(v => v.text?.toLowerCase().includes(needle));
      }
    });
  }

  function reset () {
    list.value = listFilter.value;
  }

  function getValueByLabel (label) {
    const found = list.value.find(x => x.text.toLowerCase() === label.toLowerCase());
    return found ? found.value : null;
  }

  function getLabelByValue (value) {
    // Intentional loose comparison: option values may be numbers while the
    // bound model value arrives as a string (or vice versa).
    // eslint-disable-next-line eqeqeq
    const found = list.value.find(x => x.value == value);
    return found ? found.text : null;
  }

  return {
    list,
    load,
    filter,
    reset,
    getValueByLabel,
    getLabelByValue
  };
}

export function useTagsDropdown (serviceCall) {
  const list = ref([]);
  const originalOptions = ref([]);

  async function load (...params) {
    const resp = await serviceCall(...params);

    const mapped = resp.filter(item => item && item.name)
      .map(item => ({
        text: item.name,
        value: item.id,
        bgColor: item.bgColor || "primary",
        color: item.color || "#191919"
      }));

    list.value = mapped;
    originalOptions.value = mapped;
  }

  function filter (val, update) {
    update(() => {
      const needle = val ? val.toLowerCase() : "";

      if (!needle) {
        list.value = originalOptions.value;
      } else {
        list.value = originalOptions.value.filter(v =>
          v.text.toLowerCase().includes(needle)
        );
      }
    });
  }

  return {
    list,
    load,
    filter
  };
}

function getNestedValue (obj, path) {
  if (typeof path === "function") {
    return path(obj);
  }

  if (!path || typeof path !== "string") {
    return null;
  }
  return path.split(".").reduce((acc, part) => acc?.[part], obj);
}

export function useSingleSelectDropdownWithRowIndex (serviceCall, config = {}) {
  const listByIndex = ref({});
  const listFilterByIndex = ref({});

  const {
    labelKey = "name",
    valueKey = "id",
    labelFn = null,
    dataKey = null
  } = config;

  const mapData = (resp = []) => {
    return (resp || []).map(item => ({
      text: labelFn ? labelFn(item) : getNestedValue(item, labelKey),
      value: getNestedValue(item, valueKey),
      data: dataKey ? getNestedValue(item, dataKey) : undefined
    }));
  };

  const load = async (rowIndex, ...params) => {
    const resp = await serviceCall(...params);
    const mapped = mapData(resp);
    listByIndex.value[rowIndex] = mapped;
    listFilterByIndex.value[rowIndex] = mapped;
    return mapped;
  };

  const filter = (rowIndex) => (val, update) => {
    update(() => {
      const original = listFilterByIndex.value[rowIndex] || [];

      if (!val) {
        listByIndex.value[rowIndex] = original;
        return;
      }

      const needle = val.toLowerCase();

      listByIndex.value[rowIndex] = original.filter(item =>
        item.text?.toLowerCase().includes(needle)
      );
    });
  };
  const getByIndex = (rowIndex, dependency = true) => {
    if (!dependency) return [];
    return listByIndex.value[rowIndex] || [];
  };

  const clearByIndex = (rowIndex) => {
    listByIndex.value[rowIndex] = [];
    listFilterByIndex.value[rowIndex] = [];
  };

  return {
    listByIndex,
    load,
    filter,
    getByIndex,
    clearByIndex
  };
}
