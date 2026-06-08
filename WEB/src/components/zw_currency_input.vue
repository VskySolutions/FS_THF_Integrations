<template>
  <q-input
    v-if="label" ref="inputRef" v-model="formattedValue" :label="label" outlined stack-label hide-bottom-space :dense="dense" :disable="disable"
    :error="error" :error-message="errorMessage" :readonly="readonly" :input-class="inputClass" class="text-currency"
  >
    <template v-if="append" #append>
      <slot name="append" />
    </template>
    <template v-if="prepend" #prepend>
      <slot name="prepend" />
    </template>
  </q-input>
  <q-input
    v-else ref="inputRef" v-model="formattedValue" outlined stack-label hide-bottom-space :dense="dense" :disable="disable"
    :error="error" :error-message="errorMessage" :readonly="readonly" :input-class="inputClass" class="text-currency"
  >
    <template v-if="append" #append>
      <slot name="append" />
    </template>
    <template v-if="prepend" #prepend>
      <slot name="prepend" />
    </template>
  </q-input>
</template>

<script setup>
import { useCurrencyInput } from "vue-currency-input";
import { watch } from "vue";
import _ from "lodash";

const props = defineProps({
  modelValue: {
    type: Number,
    default: null
  },

  label: {
    type: String,
    default: null
  },

  options: {
    type: Object,
    default: () => {}
  },

  disable: {
    type: Boolean,
    default: false
  },

  readonly: {
    type: Boolean,
    default: false
  },

  dense: {
    type: Boolean,
    default: false
  },

  error: {
    type: Boolean,
    default: null
  },

  errorMessage: {
    type: String,
    default: null
  },

  inputClass: {
    type: String,
    default: null
  },

  prepend: {
    type: Boolean,
    default: false
  },

  append: {
    type: Boolean,
    default: false
  },

  autofocus: {
    type: Boolean,
    default: false
  }
});

const options = _.merge({ currency: "USD" }, props.options);
const { inputRef, formattedValue, setValue } = useCurrencyInput(options);

watch(() => props.modelValue, (value) => {
  setValue(value);
});
</script>
