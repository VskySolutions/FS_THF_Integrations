<template>
  <q-card flat bordered class="app-detail-header q-mb-md">
    <q-card-section class="row items-center no-wrap q-py-sm">
      <app-breadcrumbs :items="items" no-margin class="col" />
      <q-space />
      <slot name="actions" />
      <q-btn outline no-caps color="primary" icon="o_arrow_back" label="Back" class="q-ml-sm" @click="goBack" />
    </q-card-section>
  </q-card>
</template>

<script setup>
// Standard header for internal view/manage (detail) pages: breadcrumbs on the left, a Back button
// on the right (plus an optional `actions` slot for status badges/controls). Reused on every detail
// page so the layout stays consistent across the application.
import { useRouter } from "vue-router";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";

const props = defineProps({
  items: { type: Array, default: () => [] },
  // Optional explicit destination; defaults to browser back.
  backTo: { type: [String, Object], default: null }
});

const router = useRouter();
const goBack = () => {
  if (props.backTo) router.push(props.backTo);
  else router.back();
};
</script>

<style scoped>
.app-detail-header {
  border-radius: 12px;
}
</style>
