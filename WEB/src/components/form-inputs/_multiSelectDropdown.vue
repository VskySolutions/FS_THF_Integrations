<template>
  <!-- <div class="row items-center q-mb-sm"> -->
  <div :class="props.containerClass || 'row items-center q-mb-sm'">
    <div v-if="props.label" class="col-lg-5 col-md-5 col-sm-12 col-xs-12">
      <label class="Cutomlabel q-mt-sm fs-13">{{ props.label }}</label>
    </div>

    <!-- <div
      :class="[
        props.label
          ? 'col-lg-7 col-md-7 col-sm-12 col-xs-12'
          : 'col-12'
      ]"
    > -->
    <div
      :class="[
        props.label
          ? 'col-lg-7 col-md-7 col-sm-12 col-xs-12'
          : 'col-12'
      ]"
    >
      <q-select
        v-model:input-value="inputValue"
        :model-value="props.modelValue"
        push
        class="q-mx-sm w-100 h-auto"
        clearable
        use-input
        use-chips
        transition-show="jump-up"
        transition-hide="jump-up"
        hide-bottom-space
        :dense="true"
        :disable="disable"
        multiple
        fill-input
        input-debounce="0"
        :options="props.options"
        option-value="value"
        option-label="text"
        map-options
        emit-value
        :popup-content-class="customPopupContentClass"
        @update:model-value="updateValue"
        @filter="props.filter"
        @blur="resetInput"
        @popup-hide="resetInput"
      >
        <template
          v-if="props.options?.length > 0 && props.isShowAll"
          #append
        >
          <q-checkbox
            :model-value="isAllSelected"
            @update:model-value="handleSelectAllOptions"
          >
            <q-tooltip>
              Select All
            </q-tooltip>
          </q-checkbox>
        </template>
        <template #option="{ itemProps, opt, selected, toggleOption }">
          <q-item v-bind="itemProps">
            <q-item-section>
              <div class="row q-col-gutter-x-md items-center selection-drodown-item">
                <q-checkbox
                  :model-value="selected"
                  @update:model-value="toggleOption(opt)"
                />
                <!--Tags-->
                <span
                  v-if="showBgColor"
                  :style="{
                    backgroundColor: opt.bgColor || '#e0e0e0',
                    color: opt.color || '#191919',
                    padding: '3px 8px',
                    borderRadius: '6px'
                  }"
                >
                  {{ opt.text }}
                </span>
                <span v-else>{{ opt.text }}</span>
              </div>
            </q-item-section>
          </q-item>
        </template>
      </q-select>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from "vue";

const props = defineProps({
  label: String,
  modelValue: [Array, String],
  options: Array,
  filter: Function,
  showBgColor: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  isShowAll: { type: Boolean, default: false },
  // Custom class applied to the dropdown popup content
  customPopupContentClass: {
    type: String,
    default: ""
  },
  // Custom wrapper class
  containerClass: {
    type: String,
    default: ""
  },

  // Custom input column class
  inputClass: {
    type: String,
    default: ""
  }
});

const emit = defineEmits(["update:modelValue"]);

const inputValue = ref("");

const isAllSelected = computed(() => {
  return (
    props.modelValue?.length > 0 &&
    props.options?.length > 0 &&
    props.modelValue.length === props.options.length
  );
});

function resetInput () {
  inputValue.value = "";

  if (props.filter) {
    props.filter("", (callback) => callback());
  }
}

function updateValue (val) {
  emit("update:modelValue", val);
  resetInput();
}

function handleSelectAllOptions (val) {
  if (val) {
    emit("update:modelValue", props.options.map(x => x.value));
  } else {
    emit("update:modelValue", []);
  }
}
</script>
