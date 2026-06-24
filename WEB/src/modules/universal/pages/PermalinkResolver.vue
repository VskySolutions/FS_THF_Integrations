<template>
  <q-page class="flex flex-center">
    <q-spinner color="primary" size="40px" />
  </q-page>
</template>

<script setup>
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useEntityMeta } from "composables/uf/useEntityMeta";
import { useNotify } from "composables/useNotify";

// Resolves the /entity/:type/:id permalink convention to the correct detail page.
const route = useRoute();
const router = useRouter();
const { routeFor } = useEntityMeta();
const notify = useNotify();

onMounted(() => {
  const type = Number(route.params.type);
  const id = route.params.id;
  if (!type || !id) {
    notify.error("Invalid record link.");
    router.replace({ name: "dashboard" });
    return;
  }
  router.replace(routeFor(type, id));
});
</script>
