<template>
  <div>
    <!-- Trigger + active chips -->
    <div class="row items-center q-gutter-sm q-mb-sm">
      <q-btn outline no-caps color="primary" icon="o_filter_list" label="Filters" @click="open = true">
        <q-badge v-if="activeCount" color="primary" floating>{{ activeCount }}</q-badge>
      </q-btn>
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
      <q-btn v-if="activeCount" flat dense no-caps color="grey-7" label="Clear all" @click="$emit('clear')" />
    </div>

    <q-drawer v-model="open" side="right" overlay bordered :width="360" class="column no-wrap">
      <div class="row items-center q-pa-md bg-grey-1">
        <div class="text-h6">Filters</div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="open = false" />
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
import { ref, computed } from "vue";

const props = defineProps({
  // [{ key, label }] for each active filter; drives chips + count.
  chips: { type: Array, default: () => [] }
});

defineEmits(["remove", "clear"]);

const open = ref(false);
const activeCount = computed(() => props.chips.length);
</script>
