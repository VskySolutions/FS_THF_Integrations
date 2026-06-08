<template>
  <div :class="props.wrapperClass">
    <label v-if="props.label" class="label q-mb-xs text-black">{{ props.label }}<span v-if="props.required" class="required">*</span></label>
    <div>
      <q-select
        :model-value="props.modelValue"
        :options="props.options"
        option-value="value"
        option-label="text"
        :option-disable="props.optionDisable"
        emit-value
        map-options
        :clearable="isClearable"
        use-input
        outlined
        dense
        hide-bottom-space
        :placeholder="!props.modelValue ? props.placeholder : ''"
        :disable="props.disable"
        :readonly="props.readonly"
        :error="props.error"
        :error-message="props.errorMessage"
        :bg-color="props.bgColor"
        @update:model-value="updateValue"
        @filter="props.filter"
        @popup-show="handlePopupShow"
        @blur="handleBlur"
      >
        <!-- <template #option="{ itemProps, opt }">
          <q-item v-bind="itemProps">
            <q-item-section>
              <div class="row q-col-gutter-x-md items-center">
                <span>{{ opt.text }}</span>
              </div>
            </q-item-section>
          </q-item>
        </template> -->
        <template #option="slotProps">
          <slot name="option" v-bind="slotProps">
            <!-- Default fallback -->
            <q-item v-bind="slotProps.itemProps">
              <q-item-section>
                <span>{{ slotProps.opt.text }}</span>
              </q-item-section>
            </q-item>
          </slot>
        </template>
        <template #after>
          <slot name="after" />
        </template>
      </q-select>
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  label: String,
  modelValue: [String, Number],
  options: Array,
  placeholder: {
    type: String,
    default: ""
  },
  required: {
    type: Boolean,
    default: true
  },
  isClearable: {
    type: Boolean,
    default: true
  },
  optionDisable: {
    type: [String, Function],
    default: undefined
  },
  popupShow: {
    type: Function,
    default: null
  },

  onBlur: {
    type: Function,
    default: null
  },
  disable: Boolean,
  readonly: Boolean,
  error: Boolean,
  errorMessage: String,
  filter: Function,
  bgColor: String,
  wrapperClass: {
    type: String,
    default: "col-xxl-4 col-lg-4 col-md-4 col-sm-4 col-xs-12"
  }
});

const emit = defineEmits(["update:modelValue"]);

function updateValue (val) {
  emit("update:modelValue", val);
}

function handlePopupShow () {
  props.popupShow?.();
}

function handleBlur (event) {
  props.onBlur?.(event);
}
</script>
