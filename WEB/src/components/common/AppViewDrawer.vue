<template>
  <q-drawer
    v-model="open"
    side="right"
    overlay
    bordered
    :width="width"
    class="column no-wrap"
  >
    <div class="row items-center q-pa-md bg-grey-1">
      <div class="text-h6">{{ title }}</div>
      <q-space />
      <q-btn flat round dense icon="o_close" @click="open = false" />
    </div>
    <q-separator />

    <q-scroll-area class="col">
      <div class="q-pa-md">
        <div v-for="field in fields" :key="field.label" class="row q-py-sm">
          <div class="col-5 text-grey-7">{{ field.label }}</div>
          <div class="col-7 text-weight-medium">{{ field.value ?? "—" }}</div>
        </div>
        <slot />
      </div>
    </q-scroll-area>
  </q-drawer>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: "Details" },
  width: { type: Number, default: 420 },
  fields: { type: Array, default: () => [] }
});

const emit = defineEmits(["update:modelValue"]);

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});
</script>
