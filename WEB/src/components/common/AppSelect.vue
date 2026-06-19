<template>
  <q-select
    v-model="model"
    :options="options"
    :label="label"
    :loading="loading"
    :clearable="clearable"
    :multiple="multiple"
    :option-value="optionValue"
    :option-label="optionLabel"
    :emit-value="emitValue"
    :map-options="mapOptions"
    :dense="dense"
    :use-chips="chips"
    :readonly="readonly"
    :disable="disable"
    :autocomplete="autocomplete"
    outlined
    stack-label
    hide-bottom-space
    :error="error"
    :error-message="errorMessage"
    class="app-select"
  >
    <!-- Multi-select renders each selection as a consistent badge/chip (removable when editable). -->
    <template v-if="chips" #selected-item="scope">
      <q-chip
        :removable="!readonly && !disable"
        dense
        :tabindex="scope.tabindex"
        color="blue-1"
        text-color="primary"
        class="app-select__chip"
        @remove="scope.removeAtIndex(scope.index)"
      >
        {{ scope.opt.label ?? scope.opt }}
      </q-chip>
    </template>

    <template v-if="loading" #append>
      <q-spinner size="20px" color="primary" />
    </template>
    <template #no-option>
      <q-item>
        <q-item-section class="text-grey-6">No options</q-item-section>
      </q-item>
    </template>
    <template v-if="$slots.after" #after>
      <slot name="after" />
    </template>
  </q-select>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  modelValue: { type: [String, Number, Array, Object], default: null },
  options: { type: Array, default: () => [] },
  label: { type: String, default: "" },
  loading: { type: Boolean, default: false },
  clearable: { type: Boolean, default: true },
  multiple: { type: Boolean, default: false },
  optionValue: { type: String, default: "value" },
  optionLabel: { type: String, default: "label" },
  emitValue: { type: Boolean, default: true },
  mapOptions: { type: Boolean, default: true },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  // Dense by default so selects match the dense text inputs (consistent field height).
  dense: { type: Boolean, default: true },
  // Render selections as chips/badges; defaults on for multi-select.
  useChips: { type: Boolean, default: undefined },
  readonly: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  // Disable the browser's autofill on the filter input by default (opt back in with "on").
  autocomplete: { type: String, default: "off" }
});

const emit = defineEmits(["update:modelValue"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

// Multi-select shows selections as badges by default; single-select stays inline text.
const chips = computed(() => (props.useChips === undefined ? props.multiple : props.useChips));
</script>

<style scoped>
/* Consistent control height for single-selects (matches dense text fields). Multi-selects with
   chips grow as needed but keep the same minimum. !important + higher specificity beats the
   legacy global `.q-field__control { min-height: auto !important }` rule in app.scss. */
.app-select :deep(.q-field__control) {
  min-height: 40px !important;
}
.app-select__chip {
  margin: 2px 4px 2px 0;
}
</style>
