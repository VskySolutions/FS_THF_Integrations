<template>
  <div class="app-field">
    <app-field-label :label="label" :required="required" />
    <q-select
      v-model="model"
      :options="visibleOptions"
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
      :aria-label="ariaLabel"
      :use-input="useInput"
      :input-debounce="inputDebounce"
      :fill-input="typeahead"
      :hide-selected="typeahead"
      :rules="rules"
      :hint="hint"
      outlined
      hide-bottom-space
      :error="error"
      :error-message="errorMessage"
      class="app-select"
      @input-value="search = $event || ''"
      @popup-show="search = ''"
      @popup-hide="search = ''"
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
  </div>
</template>

<script setup>
import { ref, computed, toRef } from "vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import { useFieldLabel } from "composables/useFieldLabel";

const props = defineProps({
  modelValue: { type: [String, Number, Array, Object], default: null },
  options: { type: Array, default: () => [] },
  label: { type: String, default: "" },
  // Force the required marker; a trailing "*" in the label also marks it required.
  required: { type: Boolean, default: false },
  loading: { type: Boolean, default: false },
  clearable: { type: Boolean, default: true },
  multiple: { type: Boolean, default: false },
  optionValue: { type: String, default: "value" },
  optionLabel: { type: String, default: "label" },
  emitValue: { type: Boolean, default: true },
  mapOptions: { type: Boolean, default: true },
  error: { type: Boolean, default: false },
  errorMessage: { type: String, default: "" },
  // Validation rules run by the surrounding q-form (same contract as AppTextField).
  rules: { type: Array, default: () => [] },
  // Helper line under the control (same contract as AppTextField). `hide-bottom-space` only stops the
  // space being RESERVED when there is nothing to show, so a hint still renders when one is supplied.
  hint: { type: String, default: "" },
  // Dense by default so selects match the dense text inputs (consistent field height).
  dense: { type: Boolean, default: true },
  // Render selections as chips/badges; defaults on for multi-select.
  useChips: { type: Boolean, default: undefined },
  readonly: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  // Disable the browser's autofill on the filter input by default (opt back in with "on").
  autocomplete: { type: String, default: "off" },
  // Type-to-search: opens an input inside the control and emits `filter` so the parent can narrow the
  // options (needed once a list is too long to scroll — countries, states, cities…).
  useInput: { type: Boolean, default: false },
  // 0 because the lists filtered this way are in-memory (Quasar's own default is 500ms).
  inputDebounce: { type: [Number, String], default: 0 }
});

const emit = defineEmits(["update:modelValue"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

// Multi-select shows selections as badges by default; single-select stays inline text.
const chips = computed(() => (props.useChips === undefined ? props.multiple : props.useChips));

// A searchable single-select reads as an autocomplete: the selection sits in the search input itself
// instead of beside it. Multi-select keeps its chips and a separate input.
const typeahead = computed(() => props.useInput && !props.multiple);

// Search is handled HERE rather than through QSelect's `filter` event on purpose. That event hands the
// parent a done-callback and refuses to open the menu until it is called — within a 10ms window, while
// the control still has focus. Routing that round trip through a wrapper component is what left these
// dropdowns opening empty. The options are already in memory, so narrowing them is a computed.
const search = ref("");
const optionText = (opt) =>
  String((opt !== null && typeof opt === "object" ? opt[props.optionLabel] : opt) ?? "");

const visibleOptions = computed(() => {
  const needle = props.useInput ? search.value.trim().toLowerCase() : "";
  if (needle === "") return props.options;
  return props.options.filter((opt) => optionText(opt).toLowerCase().includes(needle));
});

// Keep the control accessible now that the visible label is external.
const { text: ariaLabel } = useFieldLabel(toRef(props, "label"), toRef(props, "required"));
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
