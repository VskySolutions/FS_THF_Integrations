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
  // FALLBACK destination only — see goBack. Back returns to wherever the user actually came from; this
  // is where it lands when there is no such page.
  backTo: { type: [String, Object], default: null }
});

const router = useRouter();

// Back means "the page I came from". A detail page is reachable from several places — a list, a search,
// a notification, another record — and pushing one hardcoded route sent everyone to the same list no
// matter where they started, which also grew the history instead of unwinding it.
//
// `backTo` is the fallback for when there is genuinely nowhere to go back to: a pasted link, a fresh
// tab, a hard refresh. Vue Router's HTML5 history records the previous in-app entry on history.state,
// and `back` is null on a first load — precisely the case where router.back() would either do nothing
// (a dead button) or walk the user out of the application.
const goBack = () => {
  if (router.options.history.state?.back) router.back();
  else if (props.backTo) router.push(props.backTo);
  else router.push("/");
};
</script>

<style scoped>
.app-detail-header {
  border-radius: 12px;
}
</style>
