<template>
  <q-dialog ref="dialogRef" persistent @hide="onDialogHide">
    <q-card class="q-dialog-plugin dialog-sm">
      <q-card-section class="card-header with-tools">
        <div class="text-h2">{{ title }}</div>
        <q-btn v-close-popup icon="o_close" class="close" flat round dense />
      </q-card-section>
      <q-separator />
      <q-card-section class="card-body scroll">
        <div class="text-body2">{{ message }}</div>
        <div v-if="data" class="delete-data text-weight-600 q-mt-sm">{{ data }}</div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn color="primary" label="No" flat @click="onDialogCancel" />
        <q-btn color="negative" label="Yes" flat @click="onDialogOK" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { useDialogPluginComponent } from "quasar";

defineProps({
  title: { type: String, default: "Confirmation" },
  message: { type: String, default: "Are you sure you want to leave without saving?" },
  data: { type: String, default: "" }
});

defineEmits([
  ...useDialogPluginComponent.emits
]);

const { dialogRef, onDialogHide, onDialogOK, onDialogCancel } = useDialogPluginComponent();

</script>
