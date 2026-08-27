<template>
  <q-item clickable class="app-file-item" :disable="opening" @click="open">
    <q-item-section avatar>
      <q-spinner v-if="opening" color="primary" size="28px" />
      <q-icon v-else :name="icon" size="34px" color="primary" />
    </q-item-section>
    <q-item-section>
      <q-item-label class="ellipsis">{{ name }}</q-item-label>
      <q-item-label caption>{{ description }}</q-item-label>
    </q-item-section>
    <q-item-section side>
      <div class="row items-center no-wrap">
        <q-icon name="o_open_in_new" size="18px" color="grey-6" class="q-mr-xs">
          <q-tooltip>Open in a new tab</q-tooltip>
        </q-icon>
        <q-btn
          v-if="removable" flat round dense size="sm" icon="o_close" color="negative"
          :disable="disable" @click.stop="$emit('remove')"
        >
          <q-tooltip>Remove</q-tooltip>
        </q-btn>
      </div>
    </q-item-section>
  </q-item>
</template>

<script setup>
// Preview row for a file that is already SAVED against a record: the icon for its type, its name, its
// extension and size, and — where the form it sits on is editable — an ✕ that takes it off the record.
// Clicking the row opens the file in a new tab (see openStoredFile: the bytes come through the
// authenticated client, because a bare link to /api/media/… is refused).
//
// The staged counterpart is AppFilePreviewItem, which previews a File the browser is still holding. The
// two are deliberately the same row, so a document looks the same before and after it is saved.
import { ref, computed } from "vue";
import { getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { describeStored, iconForStored, nameOf, openStoredFile } from "composables/useFilePreview";

const props = defineProps({
  // Any stored-file shape: a REMS request file, a UF attachment, or a media response. See useFilePreview.
  file: { type: Object, required: true },
  // Whether this form may take the file off the record. Off by default: most places that list files
  // are reading them.
  removable: { type: Boolean, default: false },
  // Removal is in flight / the form is locked — the row still opens, it just cannot be removed.
  disable: { type: Boolean, default: false },
  // Where the bytes come from, for a file that is NOT in the media store — Universal Features keeps its
  // attachments behind an endpoint of its own. Default: /api/media/{mediaId}/content.
  fetchBlob: { type: Function, default: null }
});
defineEmits(["remove"]);

const notify = useNotify();
const opening = ref(false);

const icon = computed(() => iconForStored(props.file));
const name = computed(() => nameOf(props.file));
const description = computed(() => describeStored(props.file));

const open = async () => {
  if (opening.value) return;
  opening.value = true;
  try {
    await openStoredFile(props.file, props.fetchBlob);
  } catch (err) {
    notify.error(getApiErrorMessage(err, "That file could not be opened."));
  } finally {
    opening.value = false;
  }
};
</script>

<style scoped>
.app-file-item { border: 1px solid #e2e7ee; border-radius: 8px; }
</style>
