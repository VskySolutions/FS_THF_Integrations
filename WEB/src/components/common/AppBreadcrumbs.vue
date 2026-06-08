<template>
  <q-breadcrumbs class="q-mb-md text-grey-7" active-color="primary">
    <q-breadcrumbs-el
      v-for="(item, index) in items"
      :key="index"
      :label="item.label"
      :icon="item.icon"
      :to="item.to"
    />
  </q-breadcrumbs>
</template>

<script setup>
defineProps({
  // [{ label, icon?, to? }]
  items: { type: Array, default: () => [] }
});
</script>
