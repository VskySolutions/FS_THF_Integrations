<template>
  <q-input
    v-model="model"
    :label="label"
    :type="type"
    :placeholder="placeholder"
    :rules="rules"
    :error="error"
    :error-message="errorMessage"
    :disable="disable"
    :readonly="readonly"
    :hint="hint"
    :autogrow="autogrow"
    :mask="mask"
    :clearable="clearable"
    :autocomplete="autocomplete"
    outlined
    :dense="dense"
    stack-label
    hide-bottom-space
    @blur="$emit('blur', $event)"
  >
    <template v-if="$slots.prepend" #prepend>
      <slot name="prepend" />
    </template>
    <template v-if="$slots.append" #append>
      <slot name="append" />
    </template>
  </q-input>
</template>

<script setup>
// Standard single-line text/email/number field. Centralises the outlined / stack-label /
// hide-bottom-space styling so every form looks and behaves the same (UI consistency).
import { computed } from "vue";

const props = defineProps({
  modelValue: { type: [String, Number], default: "" },
  label: { type: String, default: "" },
  type: { type: String, default: "text" },
  placeholder: { type: String, default: undefined },
  rules: { type: Array, default: () => [] },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  disable: { type: Boolean, default: false },
  readonly: { type: Boolean, default: false },
  hint: { type: String, default: "" },
  autogrow: { type: Boolean, default: false },
  mask: { type: String, default: undefined },
  clearable: { type: Boolean, default: false },
  dense: { type: Boolean, default: true },
  // Browser autofill is disabled by default across the app; pass a value (e.g. "on") to opt back in.
  autocomplete: { type: String, default: "off" }
});

const emit = defineEmits(["update:modelValue", "blur"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});
</script>
