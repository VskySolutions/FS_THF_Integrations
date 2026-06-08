<template>
  <div class="row items-center justify-between no-wrap">

    <!-- Date -->
    <div class="col text-left">
      {{ modelValue }}
    </div>

    <!-- Icons -->
    <div class="column items-end">

      <!-- Edit date -->
      <q-icon
        v-if="editable"
        name="o_calendar_month"
        size="xs"
        class="cursor-pointer q-mr-xs"
      >
        <q-popup-proxy ref="popupRef" transition-show="scale" transition-hide="scale">
          <q-date
            v-model="localValue"
            mask="MM/DD/YYYY"
            :options="dateOptions"
            @update:model-value="submitDate"
          />
        </q-popup-proxy>

        <q-tooltip>Change Date</q-tooltip>
      </q-icon>

      <!-- History -->
      <q-icon
        v-if="showHistory && modelValue"
        name="o_history"
        class="cursor-pointer q-mr-xs"
        size="xs"
        @click.stop="emitHistory"
      >
        <q-tooltip>Data Change Log</q-tooltip>
      </q-icon>

    </div>

  </div>
</template>

<script setup>
import { ref, watch } from "vue";

const props = defineProps({
  rowId: [Number, String],
  modelValue: String,
  editable: Boolean,
  dateOptions: Function,

  showHistory: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits([
  "submit",
  "history"
]);

const popupRef = ref(null);
const localValue = ref(props.modelValue);

watch(() => props.modelValue, v => {
  localValue.value = v;
});

function submitDate (val) {
  emit("submit", {
    rowId: props.rowId,
    value: val
  });

  popupRef.value?.hide();
}

function emitHistory () {
  emit("history", props.rowId);
}
</script>
