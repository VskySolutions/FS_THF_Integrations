<template>
  <div class="row items-center justify-between no-wrap">

    <!-- Value / Select -->
    <div class="col">

      <template v-if="isEditing">
        <q-select
          ref="selectRef"
          v-model="localValue"
          outlined
          stack-label
          hide-bottom-space
          dense
          :disable="disable"
          :options="options"
          :option-value="optionValue"
          :option-label="optionLabel"
          :loading="loading"
          emit-value
          map-options
          :style="{ width: widthpx }"
          @filter="filter"
          @update:model-value="handleSubmit"
          @popup-show="emit('popupShow', localValue)"
          @popup-hide="cancelEdit"
        >
          <!-- Dropdown Options -->
          <template #option="scope">
            <q-item v-bind="scope.itemProps">
              <q-item-section>
                <q-badge
                  v-if="scope.opt.bgColor || scope.opt.color"
                  :style="{
                    backgroundColor: scope.opt.bgColor || '',
                    color: scope.opt.color || ''
                  }"
                  class="q-px-sm q-py-xs"
                >
                  {{ scope.opt.text }}
                </q-badge>
                <span v-else>
                  {{ scope.opt.text }}
                </span>
              </q-item-section>
            </q-item>
          </template>

          <!-- Selected Value -->
          <template #selected-item="scope">
            <q-badge
              v-if="scope.opt.bgColor || scope.opt.color"
              :style="{
                backgroundColor: scope.opt.bgColor,
                color: scope.opt.color
              }"
              class="q-px-sm q-py-xs"
            >
              {{ scope.opt.text }}
            </q-badge>
            <span v-else>
              {{ scope.opt.text }}
            </span>
          </template>
        </q-select>
      </template>

      <!-- Display -->
      <template v-else>
        <q-badge
          v-if="selectedOption && (selectedOption.bgColor || selectedOption.color)"
          :style="{
            backgroundColor: selectedOption.bgColor,
            color: selectedOption.color
          }"
          class="q-px-sm q-py-xs"
        >
          {{ displayValue }}
        </q-badge>
        <span v-else>
          {{ displayValue }}
        </span>
      </template>

      <!-- <template v-else>
        {{ displayValue }}
      </template> -->

      <q-tooltip v-if="editable">Click to edit</q-tooltip>

    </div>

    <!-- Icons -->
    <div class="col-auto">
      <div class="column items-center">
        <q-btn
          v-if="props.loading"
          flat
          :loading="loading"
          :disable="loading"
          class="q-mx-sm"
          @click="handleSubmit(localValue)"
        />

        <!-- Cancel -->
        <q-btn
          v-if="isEditing"
          icon="o_close"
          size="xs"
          color="black"
          flat
          round
          dense
          class="q-mb-xs"
          @click.stop="cancelEdit"
        >
          <q-tooltip>Cancel Edit</q-tooltip>
        </q-btn>

        <!-- History -->
        <q-icon
          v-if="showHistory && displayValue"
          name="o_history"
          size="xs"
          class="cursor-pointer"
          @click.stop="emitHistory"
        >
          <q-tooltip>Data Change Log</q-tooltip>
        </q-icon>

      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, computed, watch } from "vue";

const props = defineProps({
  field: String,
  rowId: [Number, String],
  value: [Number, String],
  displayValue: String,
  editable: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  loading: { type: Boolean, default: false },
  options: Array,
  optionValue: { type: String, default: "value" },
  optionLabel: { type: String, default: "text" },
  filter: Function,
  activeEdit: Object,
  showHistory: { type: Boolean, default: false },
  widthpx: { type: String, default: "" }
});

const emit = defineEmits([
  "submit",
  "popupShow",
  "cancel",
  "history"
]);

const selectRef = ref(null);
const localValue = ref(props.value);

watch(
  () => props.value,
  (v) => {
    localValue.value = v;
  }
);

const isEditing = computed(() => {
  return (
    props.activeEdit.rowId === props.rowId &&
    props.activeEdit.field === props.field &&
    props.editable
  );
});

function handleSubmit (val) {
  // localValue.value = val;

  emit("submit", {
    rowId: props.rowId,
    field: props.field,
    value: val
  });

  // close after success
  // selectRef.value?.hidePopup();

  // emit("cancel");
}

function cancelEdit () {
  selectRef.value?.hidePopup();
  emit("cancel", props.field);
}

function emitHistory () {
  emit("history", {
    rowId: props.rowId,
    field: props.field
  });
}

const selectedOption = computed(() => {
  return props.options?.find(
    x => x.value === props.value
  );
});
</script>
