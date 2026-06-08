<template>
  <div class="row items-center no-wrap">

    <!-- Search Input -->
    <q-input
      v-model="model"
      :loading="loading"
      outlined
      dense
      clearable
      debounce="500"
      placeholder="Search"
      class="bg-white search-box"
    >
      <template #prepend>
        <q-icon name="o_search" />
      </template>
    </q-input>

    <!-- Filter Button -->
    <q-btn
      unelevated
      :color="filterCount > 0 ? 'primary' : 'grey-7'"
      text-color="white"
      class="q-pa-xs filter-btn"
      style="height: 40px; border-top-left-radius: 0; border-bottom-left-radius: 0;"
      @click.stop="$emit('toggle-filter')"
    >
      <q-badge
        v-if="filterCount > 0"
        color="green"
        floating
      >
        {{ filterCount }}
      </q-badge>

      <q-icon name="o_filter_alt" size="sm" class="q-mr-xs" />

      <q-item-label class="text-xs fs-12">
        <span class="block">Set/Clear</span>
        <span class="block">FILTER</span>
      </q-item-label>

      <q-tooltip anchor="bottom middle" self="top middle">
        Advanced Filter
      </q-tooltip>
    </q-btn>

  </div>
</template>

<script setup>
import { computed } from "vue";

const props = defineProps({
  modelValue: String,
  loading: Boolean,
  appliedFilters: {
    type: Object,
    default: () => ({})
  }
});

const emit = defineEmits([
  "update:modelValue",
  "toggle-filter"
]);

const model = computed({
  get: () => props.modelValue,
  set: val => emit("update:modelValue", val)
});

const filterCount = computed(() => Object.keys(props.appliedFilters).length);
</script>
