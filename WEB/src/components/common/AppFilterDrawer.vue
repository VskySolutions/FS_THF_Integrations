<template>
  <div>
    <!-- Active filter chips (above the table; the Filters trigger lives in AppListHeader). -->
    <div v-if="chips.length" class="row items-center q-gutter-sm q-mb-sm">
      <q-chip
        v-for="chip in chips"
        :key="chip.key"
        removable
        color="blue-1"
        text-color="primary"
        @remove="$emit('remove', chip.key)"
      >
        {{ chip.label }}
      </q-chip>
      <q-btn flat dense no-caps color="grey-7" label="Clear all" @click="$emit('clear')" />
    </div>

    <q-drawer v-model="open" side="right" overlay bordered :width="360" class="column no-wrap">
      <div class="row items-center q-pa-md bg-primary text-white">
        <div class="text-h6">Filters</div>
        <q-space />
        <q-btn flat round dense color="white" icon="o_close" @click="open = false" />
      </div>
      <q-separator />
      <q-scroll-area class="col">
        <div class="q-pa-md">
          <slot />
        </div>
      </q-scroll-area>
      <q-separator />
      <div class="row justify-end q-gutter-sm q-pa-md bg-grey-1">
        <q-btn flat no-caps color="grey-8" label="Clear all" @click="$emit('clear')" />
        <q-btn unelevated no-caps color="primary" label="Done" @click="open = false" />
      </div>
    </q-drawer>
  </div>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // [{ key, label }] for each active filter; drives the chips.
  chips: { type: Array, default: () => [] }
});

const emit = defineEmits(["update:modelValue", "remove", "clear"]);

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});
</script>
