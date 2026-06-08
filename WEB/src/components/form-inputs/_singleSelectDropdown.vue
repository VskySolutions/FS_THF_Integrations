<template>
  <div class="row items-center q-mb-sm">
    <div v-if="props.label" class="col-lg-5 col-md-5 col-sm-12 col-xs-12">
      <label class="Cutomlabel q-mt-sm fs-13">{{ props.label }}</label>
    </div>

    <div
      :class="[
        props.label
          ? 'col-lg-7 col-md-7 col-sm-12 col-xs-12'
          : 'col-12'
      ]"
    >
      <q-select
        :model-value="props.modelValue"
        class="q-mx-sm w-100 h-auto"
        clearable
        use-input
        use-chips
        transition-show="jump-up"
        transition-hide="jump-up"
        hide-bottom-space
        :dense="true"
        input-debounce="0"
        :options="props.options"
        option-value="value"
        option-label="text"
        emit-value
        map-options
        :disable="props.disable"
        @update:model-value="updateValue"
        @filter="props.filter"
      >
        <template #option="{ itemProps, opt }">
          <q-item v-bind="itemProps">
            <q-item-section>
              <div class="row q-col-gutter-x-md items-center selection-drodown-item">
                <span>{{ opt.text }}</span>
              </div>
            </q-item-section>
          </q-item>
        </template>
      </q-select>
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  label: String,
  modelValue: [Array, String],
  options: Array,
  filter: Function,
  disable: Boolean
});

const emit = defineEmits(["update:modelValue"]);

function updateValue (val) {
  emit("update:modelValue", val);
}
</script>
