<template>
  <q-input
    v-if="label" ref="inputRef" v-model="value" :label="label" stack-label outlined hide-bottom-space :dense="dense" :disable="disable"
    :mask="mask" :error="error" :error-message="errorMessage" :readonly="readonly" :input-class="inputClass" class="text-date"
  >
    <template #append>
      <q-icon name="o_calendar_month" class="cursor-pointer">
        <q-popup-proxy ref="qDateProxy" transition-show="scale" transition-hide="scale" @before-show="updateProxyDate()">
          <q-date v-model="proxyDate" mask="MM/DD/YYYY" :options="options" @update:model-value="() => updateModelValue()" />
        </q-popup-proxy>
        <q-tooltip v-if="tooltip">
          <slot name="tooltip" />
        </q-tooltip>
      </q-icon>
    </template>
    <template v-if="prepend" #prepend>
      <slot name="prepend" />
    </template>
  </q-input>
  <q-input
    v-else ref="inputRef" v-model="value" stack-label outlined hide-bottom-space :dense="dense" :disable="disable" :mask="mask"
    :error="error" :error-message="errorMessage" :readonly="readonly" :input-class="inputClass" class="text-date"
  >
    <template #append>
      <q-icon name="o_calendar_month" class="cursor-pointer">
        <q-popup-proxy ref="qDateProxy" transition-show="scale" transition-hide="scale" @before-show="updateProxyDate()">
          <q-date v-model="proxyDate" mask="MM/DD/YYYY" :options="options" @update:model-value="() => updateModelValue()" />
        </q-popup-proxy>
      </q-icon>
    </template>
    <template v-if="prepend" #prepend>
      <slot name="prepend" />
    </template>
  </q-input>
</template>

<script setup>
import { ref, watch, computed } from "vue";
import useFilters from "composables/useFilters";

const { toDate } = useFilters();

const $emit = defineEmits(["update:modelValue"]);

const props = defineProps({
  modelValue: {
    type: String,
    default: null
  },

  label: {
    type: String,
    default: null
  },

  // eslint-disable-next-line vue/require-default-prop
  options: {
    type: Function
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

  mask: {
    type: String,
    default: "##/##/####"
  },

  prepend: {
    type: Boolean,
    default: false
  },

  tooltip: {
    type: Boolean,
    default: false
  },

  defaultDate: {
    type: String,
    default: null
  }
});

const proxyDate = ref(null);
const proxyDate1 = ref(null);
const qDateProxy = ref(null);

const value = computed({
  get: () => props.modelValue,
  set: (newValue) => {
    $emit("update:modelValue", newValue);
  }
});

const updateProxyDate = () => {
  if (value.value) {
    proxyDate.value = toDate(value.value);
  } else {
    if (props.defaultDate) {
      proxyDate.value = toDate(props.defaultDate);
      proxyDate1.value = proxyDate.value;
    } else {
      proxyDate.value = toDate(new Date());
      proxyDate1.value = proxyDate.value;
    }
  }
};

const updateModelValue = () => {
  if (!proxyDate.value && !value.value) {
    value.value = proxyDate1.value;
  } else {
    value.value = proxyDate.value;
  }
  qDateProxy.value.hide();
};

watch(() => props.modelValue, updateProxyDate);
</script>
