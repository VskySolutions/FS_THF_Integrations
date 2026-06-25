<template>
  <div class="app-field">
    <app-field-label :label="label" :required="required" />
    <q-input
      v-model="model"
      :aria-label="ariaLabel"
      outlined
      :dense="dense"
      hide-bottom-space
      clearable
      readonly
      :error="hasError"
      :error-message="errorMessage"
    >
      <template #prepend>
        <q-icon name="o_event" class="cursor-pointer">
          <q-popup-proxy cover transition-show="scale" transition-hide="scale">
            <q-date v-model="model" mask="YYYY-MM-DD" :options="dateOptions" today-btn>
              <div class="row items-center justify-end">
                <q-btn v-close-popup label="Close" color="primary" flat no-caps />
              </div>
            </q-date>
          </q-popup-proxy>
        </q-icon>
      </template>
    </q-input>
  </div>
</template>

<script setup>
import { computed, toRef } from "vue";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import { useFieldLabel } from "composables/useFieldLabel";

const props = defineProps({
  modelValue: { type: String, default: null },
  label: { type: String, default: "" },
  required: { type: Boolean, default: false },
  // Dense by default so date fields match the dense selects/inputs (consistent field height).
  dense: { type: Boolean, default: true },
  // Start/End range validation: only one of these is typically supplied.
  minDate: { type: String, default: null },
  maxDate: { type: String, default: null }
});

const emit = defineEmits(["update:modelValue"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const { text: ariaLabel } = useFieldLabel(toRef(props, "label"), toRef(props, "required"));

// Constrain selectable days so Start <= End.
const dateOptions = (date) => {
  const d = date.replace(/\//g, "-");
  if (props.minDate && d < props.minDate) {
    return false;
  }
  if (props.maxDate && d > props.maxDate) {
    return false;
  }
  return true;
};

const hasError = computed(() =>
  !!(props.modelValue && props.minDate && props.modelValue < props.minDate) ||
  !!(props.modelValue && props.maxDate && props.modelValue > props.maxDate));

const errorMessage = computed(() => (hasError.value ? "Start date must be on or before end date." : ""));
</script>
