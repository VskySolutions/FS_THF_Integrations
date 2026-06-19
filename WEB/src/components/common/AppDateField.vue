<template>
  <q-input
    v-model="model"
    :label="label"
    type="date"
    :rules="rules"
    :error="error"
    :error-message="errorMessage"
    :disable="disable"
    :readonly="readonly"
    :autocomplete="autocomplete"
    outlined
    :dense="dense"
    stack-label
    hide-bottom-space
  />
</template>

<script setup>
// Standard date field (ISO yyyy-mm-dd). Wraps q-input so date pickers stay consistent.
import { computed } from "vue";

const props = defineProps({
  modelValue: { type: String, default: "" },
  label: { type: String, default: "" },
  rules: { type: Array, default: () => [] },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  disable: { type: Boolean, default: false },
  readonly: { type: Boolean, default: false },
  dense: { type: Boolean, default: true },
  // Browser autofill is disabled by default across the app; pass "on" to opt back in.
  autocomplete: { type: String, default: "off" }
});

const emit = defineEmits(["update:modelValue"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});
</script>
