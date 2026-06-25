<template>
  <app-select
    :model-value="modelValue"
    :options="options"
    :label="label"
    :loading="loading"
    :clearable="clearable"
    :multiple="multiple"
    :dense="dense"
    :disable="disable"
    :error="error"
    :error-message="errorMessage"
    @update:model-value="$emit('update:modelValue', $event)"
  />
</template>

<script setup>
import AppSelect from "components/common/AppSelect.vue";
import { useOptionSetOptions } from "composables/useOptionSet";

// Reusable dropdown backed by a tenant-configurable option list. Resolves the effective values for
// a (entityType, key) and, for cascading lists, re-resolves when `parentItemId` changes.
const props = defineProps({
  modelValue: { type: [String, Number, Array, Object], default: null },
  // EntityType enum value the list belongs to.
  entityType: { type: Number, required: true },
  // The list's stable key, e.g. "payment_terms".
  optionKey: { type: String, required: true },
  // For a dependency list, the selected parent item's id (filters the values).
  parentItemId: { type: String, default: null },
  label: { type: String, default: "" },
  clearable: { type: Boolean, default: true },
  multiple: { type: Boolean, default: false },
  dense: { type: Boolean, default: true },
  disable: { type: Boolean, default: false },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" }
});

defineEmits(["update:modelValue"]);

const { options, loading } = useOptionSetOptions(() => ({
  entityType: props.entityType,
  key: props.optionKey,
  parentItemId: props.parentItemId
}));
</script>
