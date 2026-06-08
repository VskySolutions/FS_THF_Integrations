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
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn v-if="cancel" color="primary" :label="cancelLabel" flat @click="onDialogCancel" />
        <q-btn color="primary" :label="okLabel" flat @click="onDialogOK" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { useDialogPluginComponent } from "quasar";

defineProps({
  title: { type: String, default: "Confirmation" },
  message: { type: String, default: "Are you sure you want to do this?" },
  cancel: { type: Boolean, default: true },
  cancelLabel: { type: String, default: "Cancel" },
  okLabel: { type: String, default: "Ok" }
});

defineEmits([
  ...useDialogPluginComponent.emits
]);

const { dialogRef, onDialogHide, onDialogOK, onDialogCancel } = useDialogPluginComponent();

</script>
