<template>
  <div class="app-field">
    <app-field-label :label="label" :required="required" />
    <q-input
      v-model="model"
      type="date"
      :rules="rules"
      :error="error"
      :error-message="errorMessage"
      :disable="disable"
      :readonly="readonly"
      :autocomplete="autocomplete"
      :aria-label="ariaLabel"
      outlined
      :dense="dense"
      hide-bottom-space
    />
  </div>
</template>

<script setup>
// Standard date field (ISO yyyy-mm-dd). Wraps q-input with the external top-left label so date
// pickers stay consistent with the rest of the form.
import { computed, toRef } from "vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import { useFieldLabel } from "composables/useFieldLabel";

const props = defineProps({
  modelValue: { type: String, default: "" },
  label: { type: String, default: "" },
  required: { type: Boolean, default: false },
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

const { text: ariaLabel } = useFieldLabel(toRef(props, "label"), toRef(props, "required"));
</script>
