<template>
  <div class="row items-center no-wrap q-gutter-sm q-mb-md app-list-header">
    <app-breadcrumbs v-if="breadcrumbs.length" :items="breadcrumbs" no-margin />
    <q-space />

    <q-input
      v-if="showSearch"
      :model-value="search"
      dense
      outlined
      debounce="300"
      :placeholder="searchPlaceholder"
      class="app-list-header__search"
      @update:model-value="$emit('update:search', $event)"
    >
      <template #prepend><q-icon name="o_search" /></template>
    </q-input>

    <q-btn v-if="showFilters" outline no-caps color="primary" icon="o_filter_list" label="Filters" @click="$emit('filters')">
      <q-badge v-if="filterCount" floating color="primary">{{ filterCount }}</q-badge>
    </q-btn>

    <q-btn v-if="showAdd" unelevated no-caps color="primary" :icon="addIcon" :label="addLabel" :disable="addDisable" @click="$emit('add')" />

    <q-btn v-if="showBack" flat no-caps color="primary" icon="o_arrow_back" label="Back" @click="$emit('back')" />
  </div>
</template>

<script setup>
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";

defineProps({
  breadcrumbs: { type: Array, default: () => [] },
  search: { type: String, default: "" },
  searchPlaceholder: { type: String, default: "Search" },
  showSearch: { type: Boolean, default: false },
  showFilters: { type: Boolean, default: false },
  filterCount: { type: Number, default: 0 },
  showAdd: { type: Boolean, default: false },
  addLabel: { type: String, default: "Add" },
  addIcon: { type: String, default: "o_add" },
  addDisable: { type: Boolean, default: false },
  showBack: { type: Boolean, default: false }
});

defineEmits(["update:search", "filters", "add", "back"]);
</script>

<style scoped>
.app-list-header__search {
  max-width: 260px;
  width: 100%;
}
</style>
