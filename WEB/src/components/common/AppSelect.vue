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
    outlined
    stack-label
    hide-bottom-space
    :error="error"
    :error-message="errorMessage"
  >
    <template v-if="loading" #append>
      <q-spinner size="20px" color="primary" />
    </template>
    <template #no-option>
      <q-item>
        <q-item-section class="text-grey-6">No options</q-item-section>
      </q-item>
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
  errorMessage: { type: String, default: "" }
});

const emit = defineEmits(["update:modelValue"]);

const model = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});
</script>
