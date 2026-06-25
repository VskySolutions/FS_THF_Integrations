<template>
  <div class="app-field">
    <app-field-label :label="label" :required="required" />

    <div class="row items-center q-gutter-md">
      <q-avatar :size="size" color="grey-3" text-color="grey-8">
        <img v-if="modelValue" :src="modelValue" alt="">
        <q-icon v-else :name="placeholderIcon" size="48px" />
      </q-avatar>
      <div class="column q-gutter-sm">
        <div class="row q-gutter-sm">
          <q-btn outline no-caps color="primary" icon="o_upload" :label="buttonLabel" :disable="disable" @click="pick" />
          <q-btn v-if="modelValue" flat no-caps color="negative" icon="o_delete" label="Remove" :disable="disable" @click="remove" />
        </div>
        <div v-if="hint" class="app-image-upload__hint">{{ hint }}</div>
      </div>
    </div>

    <input ref="inputRef" type="file" :accept="accept" class="hidden" @change="onSelected">

    <!-- Crop dialog -->
    <q-dialog v-model="cropOpen">
      <q-card style="min-width: 360px; max-width: 90vw;">
        <q-card-section class="text-subtitle1 text-weight-medium">Crop image</q-card-section>
        <q-separator />
        <q-card-section>
          <cropper ref="cropperRef" :src="cropSrc" :stencil-props="{ aspectRatio }" class="app-image-upload__cropper" />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" @click="cropOpen = false" />
          <q-btn unelevated no-caps color="primary" label="Apply" :loading="loading" @click="applyCrop" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
// Standard image upload with cropping (vue-advanced-cropper) and the external top-left label. The
// avatar/preview is driven by v-model (the current image URL). Picking an image opens the crop dialog;
// applying it emits `crop` with the cropped PNG File — the parent uploads it and sets the new URL via
// v-model, then calls the exposed closeCrop(). Used for avatars/photos (square by default).
import { ref } from "vue";
import { Cropper } from "vue-advanced-cropper";
import "vue-advanced-cropper/dist/style.css";
import AppFieldLabel from "components/common/AppFieldLabel.vue";

const props = defineProps({
  // The current image URL shown in the preview avatar.
  modelValue: { type: String, default: null },
  label: { type: String, default: "" },
  required: { type: Boolean, default: false },
  aspectRatio: { type: Number, default: 1 },
  size: { type: String, default: "96px" },
  accept: { type: String, default: "image/*" },
  buttonLabel: { type: String, default: "Upload" },
  placeholderIcon: { type: String, default: "o_person" },
  hint: { type: String, default: "" },
  // True while the parent is uploading the cropped file.
  loading: { type: Boolean, default: false },
  disable: { type: Boolean, default: false },
  // File name given to the cropped image.
  fileName: { type: String, default: "image.png" }
});
const emit = defineEmits(["update:modelValue", "crop", "remove"]);

const inputRef = ref(null);
const cropperRef = ref(null);
const cropOpen = ref(false);
const cropSrc = ref(null);

const pick = () => { if (!props.disable) inputRef.value?.click(); };

const onSelected = (e) => {
  const file = e.target.files?.[0];
  e.target.value = ""; // allow re-selecting the same file
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => { cropSrc.value = reader.result; cropOpen.value = true; };
  reader.readAsDataURL(file);
};

const applyCrop = () => {
  const result = cropperRef.value?.getResult();
  if (!result?.canvas) { cropOpen.value = false; return; }
  result.canvas.toBlob((blob) => {
    if (!blob) { cropOpen.value = false; return; }
    emit("crop", new File([blob], props.fileName, { type: "image/png" }));
  }, "image/png");
};

const remove = () => {
  emit("update:modelValue", null);
  emit("remove");
};

// The parent closes the dialog once its upload completes.
defineExpose({ closeCrop: () => { cropOpen.value = false; } });
</script>

<style scoped>
.app-image-upload__hint { font-size: 12px; color: #8a94a3; }
.app-image-upload__cropper { max-height: 60vh; }
</style>
