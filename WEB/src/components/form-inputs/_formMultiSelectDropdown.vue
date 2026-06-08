<template>
  <div :class="props.wrapperClass">
    <label v-if="props.label" class="label q-mb-xs text-black">
      {{ props.label }}<span v-if="props.required" class="required">*</span>
    </label>
    <div>
      <q-select
        :model-value="props.modelValue"
        :options="props.options"
        option-value="value"
        option-label="text"
        emit-value
        map-options
        multiple
        use-chips
        use-input
        fill-input
        clearable
        outlined
        dense
        hide-bottom-space
        input-debounce="0"
        :disable="props.disable"
        :readonly="props.readonly"
        :error="props.error"
        :error-message="props.errorMessage"
        :popup-content-class="props.popupContentClass"
        @update:model-value="updateValue"
        @filter="props.filter"
        @blur="props.onBlur"
      >
        <!-- Custom Option -->
        <template #option="{ itemProps, opt, selected, toggleOption }">
          <q-item v-bind="itemProps">
            <q-item-section>
              <div class="row q-col-gutter-x-md items-center" style="white-space: normal; overflow-wrap: break-word;">
                <q-checkbox
                  :model-value="selected"
                  @update:model-value="toggleOption(opt)"
                />
                <span>{{ opt.text }}</span>
              </div>
            </q-item-section>
          </q-item>
        </template>

        <!-- After Slot -->
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
  modelValue: {
    type: Array,
    default: () => []
  },
  options: Array,
  required: {
    type: Boolean,
    default: true
  },
  disable: Boolean,
  readonly: Boolean,
  error: Boolean,
  errorMessage: String,
  filter: Function,
  onBlur: Function,
  popupContentClass: String,
  wrapperClass: {
    type: String,
    default: "col-xxl-4 col-lg-4 col-md-4 col-sm-4 col-xs-12"
  }
});

const emit = defineEmits(["update:modelValue"]);

function updateValue (val) {
  emit("update:modelValue", val || []);
}
</script>
