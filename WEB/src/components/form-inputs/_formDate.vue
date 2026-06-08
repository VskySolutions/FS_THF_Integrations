<template>
  <div :class="props.wrapperClass">
    <label v-if="props.label" class="label q-mb-xs text-black">
      {{ props.label }}<span v-if="props.required" class="required">*</span>
    </label>

    <div>
      <q-input
        :model-value="props.modelValue"
        outlined
        dense
        hide-bottom-space
        mask="##/##/####"
        :disable="props.disable"
        :readonly="props.readonly"
        :error="props.error"
        :error-message="props.errorMessage"
        @update:model-value="updateValue"
        @blur="props.onBlur"
      >
        <template #append>
          <q-icon
            name="o_calendar_month"
            :class="['cursor-pointer', { 'disabled': props.readonly || props.disable }]"
          >
            <q-popup-proxy
              v-if="!props.readonly && !props.disable"
              ref="qDateProxy"
              transition-show="scale"
              transition-hide="scale"
            >
              <q-date
                :model-value="props.modelValue"
                mask="MM/DD/YYYY"
                :options="props.dateOptions"
                :navigation-min-year-month="props.navigationMinYearMonth || undefined"
                :minimal="props.minimal || false"
                :first-day-of-week="props.firstDayOfWeek ?? undefined"
                @update:model-value="onDateSelect"
              />
            </q-popup-proxy>
          </q-icon>
        </template>

        <template #after>
          <slot name="after" />
        </template>
      </q-input>
    </div>
  </div>
</template>

<script setup>
import { ref } from "vue";

const qDateProxy = ref(null);

const props = defineProps({
  label: String,
  modelValue: String,
  required: {
    type: Boolean,
    default: true
  },
  disable: Boolean,
  readonly: Boolean,
  error: Boolean,
  errorMessage: String,
  onBlur: Function,
  dateOptions: Function,
  navigationMinYearMonth: {
    type: String,
    default: null
  },

  minimal: {
    type: Boolean,
    default: false
  },

  firstDayOfWeek: {
    type: Number,
    default: undefined
  },
  wrapperClass: {
    type: String,
    default: "col-xxl-3 col-lg-3 col-md-3 col-sm-3 col-xs-12"
  }
});

const emit = defineEmits(["update:modelValue"]);

function updateValue (val) {
  emit("update:modelValue", val);
}

function onDateSelect (val) {
  emit("update:modelValue", val);
  emit("update:model-value", val);
  qDateProxy.value.hide();
}
</script>
