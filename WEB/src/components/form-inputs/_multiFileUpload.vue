<template>
  <div>
    <q-uploader
      ref="uploaderRef"
      class="prodUploader"
      color="white"
      text-color="dark"
      hide-upload-btn
      multiple
      flat
      bordered
      :label="label"
      :style="{
        minHeight: height ? height + 'px' : '120px',
        maxHeight: height ? height + 'px' : 'none',
        width: '100%'
      }"
      @added="onFileAdded"
    />
    <div class="text-grey-7 text-caption q-mt-xs">
      <i v-if="allowedText">Allowed: {{ allowedText }}</i>
    </div>
    <div class="text-grey-7 text-caption q-mt-xs">
      <i v-if="maxSizeInMb">Max size: {{ maxSizeInMb }} MB</i>
    </div>
    <!-- File List -->
    <!-- <div v-if="filesData.length" class="q-mt-sm">
      <div
        v-for="(file, index) in filesData"
        :key="index"
        class="row items-center justify-between q-pa-xs bg-grey-2 q-mb-xs"
      >
        <div>{{ file.name }}</div>
        <q-btn
          icon="delete"
          color="negative"
          flat
          dense
          @click="remove(file)"
        />
      </div>
    </div> -->
  </div>
</template>

<script setup>
import { computed } from "vue";
import { useMultiFileUpload } from "src/composables/form-inputs/useMultiFileUpload";
import { notifyError } from "assets/utils";

// ----------------------------------------------------------------------------------------------------------------
// Define props
// ----------------------------------------------------------------------------------------------------------------

const props = defineProps({
  // allowedTypes: Array,
  allowedExtensions: Array,
  maxSizeInMb: {
    type: Number,
    default: 25 // default 25 MB
  },
  label: String,
  height: Number,
  initialFiles: Array // for existing files
});

// ----------------------------------------------------------------------------------------------------------------
// Define emits
// ----------------------------------------------------------------------------------------------------------------

// const emit = defineEmits(["files-selected"]);
const emit = defineEmits(["files-selected", "files-valid"]);

const { uploaderRef, onFileAdded } =
  useMultiFileUpload({
    ...props,
    onSuccess: (newFiles, allFiles, isValid) => {
      emit("files-selected", allFiles);
      emit("files-valid", isValid);
    },
    onError: (msg) => {
      notifyError({ message: msg });
      emit("files-valid", false);
    }
  });

const allowedText = computed(() => {
  return props.allowedExtensions?.join(", ");
});

// ----------------------------------------------------------------------------------------------------------------
// Remove files
// ----------------------------------------------------------------------------------------------------------------

// function remove (file) {
//   removeFile(file);
//   emit("files-selected", filesData.value);
// }
</script>
