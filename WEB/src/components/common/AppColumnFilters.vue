<template>
  <div class="column q-gutter-sm">
    <template v-for="col in columns" :key="col.name">
      <app-select
        v-if="col.filterOptions"
        v-model="filters[col.name]"
        :options="col.filterOptions"
        :label="col.label"
      />
      <app-text-field
        v-else
        v-model="filters[col.name]"
        :label="col.label"
        clearable
        :dense="false"
      />
    </template>
  </div>
</template>

<script setup>
// Renders one filter control per filterable column (select when the column declares filterOptions,
// otherwise a text "contains" box). Pairs with the useColumnFilters composable; reused by every
// list's filter drawer so filtering looks and behaves the same everywhere.
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

// Shared via v-model; nested writes go back to the caller's reactive filters object.
const filters = defineModel({ type: Object, required: true });

defineProps({
  columns: { type: Array, default: () => [] }
});
</script>
