<template>
  <div>
    <div v-if="!previewUrl">
      <q-uploader
        ref="uploaderRef"
        color="white"
        text-color="dark"
        flat
        bordered
        hide-upload-btn
        :label="label"
        style="min-height: 120px; width: 100%"
        @added="onFileAdded"
      />
      <div class="text-grey-7 text-caption q-mt-xs">
        <div v-if="allowedText">
          <i>Allowed: {{ allowedText }}</i>
        </div>
        <div v-if="maxSizeInMb">
          <i>Max size: {{ maxSizeInMb }} MB</i>
        </div>
        <!--This imageHeight & imageSize only for images-->
        <div v-if="imageSize && imageHeight">
          <i>{{ imageSize }} x {{ imageHeight }} px required</i>
        </div>
      </div>
    </div>
    <div v-if="previewUrl" class="q-mt-sm row items-center q-gutter-sm">
      <!-- Preview -->
      <div>
        <img v-if="isImage" :src="previewUrl" style="width: 120px;">
        <div v-else>{{ fileName }}</div>
      </div>
      <!-- Remove Button -->
      <q-btn
        color="negative"
        label="Remove"
        outline
        no-caps
        @click="removeFile"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, watch, computed } from "vue";
import { useSingleFileUpload } from "src/composables/form-inputs/useSingleFileUpload";
import { notifyWarning } from "assets/utils";

// ----------------------------------------------------------------------------------------------------------------
// Define props
// ----------------------------------------------------------------------------------------------------------------

const props = defineProps({
  allowedTypes: Array,
  allowedExtensions: Array,
  maxSizeInMb: {
    type: Number,
    default: 25 // default 25 MB
  },
  imageSize: Number,
  imageHeight: Number,
  isImage: Boolean,
  label: String,
  initialUrl: String
});

// ----------------------------------------------------------------------------------------------------------------
// Define emits
// ----------------------------------------------------------------------------------------------------------------

// const emit = defineEmits(["file-selected"]);
const emit = defineEmits(["file-selected", "file-valid"]);

// ----------------------------------------------------------------------------------------------------------------
// Common variables
// ----------------------------------------------------------------------------------------------------------------

const previewUrl = ref(props.initialUrl || null);
const fileName = ref("");

const { onFileAdded, reset } = useSingleFileUpload({
  ...props,
  onSuccess: (file) => {
    emit("file-selected", file);
    emit("file-valid", true);

    if (props.isImage) {
      previewUrl.value = URL.createObjectURL(file);
    } else {
      fileName.value = file.name;
    }
  },
  onError: (msg) => {
    notifyWarning({ message: msg });
    emit("file-valid", false);
  }
});

const allowedText = computed(() => {
  return props.allowedExtensions?.join(", ");
});

// ----------------------------------------------------------------------------------------------------------------
// Remove file
// ----------------------------------------------------------------------------------------------------------------

function removeFile () {
  reset();
  previewUrl.value = null;
  fileName.value = "";
  emit("file-selected", null);
  emit("file-valid", true);
}

watch(() => props.initialUrl, (val) => {
  previewUrl.value = val;
});
</script>
