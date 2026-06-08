<!-- src/components/form-inputs/_formMonthYearPicker.vue -->
<template>
  <div :class="wrapperClass">
    <label v-if="label" class="label q-mb-xs text-black">
      {{ label }}<span v-if="required" class="required">*</span>
    </label>

    <q-input
      :model-value="modelValue"
      fill-input
      dense
      outlined
      hide-bottom-space
      :disable="disable"
      :readonly="readonly"
      :error="error"
      :error-message="errorMessage"
      @update:model-value="updateValue"
      @blur="onBlur"
    >
      <template #append>
        <q-icon
          name="o_calendar_month"
          :class="['cursor-pointer', { disabled: readonly || disable }]"
        >
          <q-popup-proxy
            v-if="!readonly && !disable"
            ref="qDateProxy"
            transition-show="scale"
            transition-hide="scale"
          >
            <q-date
              ref="calendarRef"
              :model-value="internalValue"
              mask="MMMM-YYYY"
              emit-immediately
              :default-view="currentInternalView"
              :navigation-min-year-month="navigationMinYearMonth || undefined"
              @update:model-value="onMonthSelect"
            />
          </q-popup-proxy>
        </q-icon>
      </template>

      <template #after>
        <slot name="after" />
      </template>
    </q-input>
  </div>
</template>

<script setup>
import { ref, watch } from "vue";

const props = defineProps({
  label: {
    type: String,
    default: ""
  },

  modelValue: {
    type: String,
    default: ""
  },

  required: {
    type: Boolean,
    default: true
  },

  disable: {
    type: Boolean,
    default: false
  },

  readonly: {
    type: Boolean,
    default: false
  },

  error: {
    type: Boolean,
    default: false
  },

  errorMessage: {
    type: String,
    default: ""
  },

  onBlur: {
    type: Function,
    default: null
  },

  navigationMinYearMonth: {
    type: String,
    default: null
  },

  defaultView: {
    type: String,
    default: "Years"
  },

  wrapperClass: {
    type: String,
    default: "col-xxl-3 col-lg-3 col-md-3 col-sm-3 col-xs-12"
  }
});

const emit = defineEmits([
  "update:modelValue",
  "update:model-value"
]);

const qDateProxy = ref(null);
const calendarRef = ref(null);

const internalValue = ref("");
const currentInternalView = ref(props.defaultView);

watch(
  () => props.modelValue,
  (val) => {
    internalValue.value = val || "";
  },
  { immediate: true }
);

watch(
  () => props.defaultView,
  (val) => {
    currentInternalView.value = val || "Years";
  }
);

function updateValue (val) {
  emit("update:modelValue", val);
  emit("update:model-value", val);
}

function resetCalendarView () {
  currentInternalView.value = "Years";
}

function goToMonthView () {
  currentInternalView.value = "Months";
  calendarRef.value?.setView("Months");
}

function onMonthSelect (val) {
  if (currentInternalView.value === "Years") {
    goToMonthView();
    return;
  }

  internalValue.value = val;

  emit("update:modelValue", val);
  emit("update:model-value", val);

  resetCalendarView();

  qDateProxy.value?.hide();
}
</script>
