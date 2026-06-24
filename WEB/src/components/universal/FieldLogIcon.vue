<template>
  <span v-if="count > 0" class="uf-field-log-icon">
    <q-btn flat dense round size="sm" icon="o_history" color="grey-7" @click="drawerOpen = true">
      <q-tooltip>Change history</q-tooltip>
      <q-badge v-if="count > 5" floating color="primary" :label="count" rounded />
    </q-btn>
    <field-modified-log-drawer
      v-model="drawerOpen"
      :entity-type="entityType"
      :entity-id="entityId"
      :field-name="fieldName"
      :field-label="fieldLabel || fieldName"
    />
  </span>
</template>

<script setup>
import { ref } from "vue";
import FieldModifiedLogDrawer from "./FieldModifiedLogDrawer.vue";

defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true },
  fieldName: { type: String, required: true },
  fieldLabel: { type: String, default: "" },
  count: { type: Number, default: 0 }
});

const drawerOpen = ref(false);
</script>

<style scoped>
.uf-field-log-icon { display: inline-flex; vertical-align: middle; }
</style>
