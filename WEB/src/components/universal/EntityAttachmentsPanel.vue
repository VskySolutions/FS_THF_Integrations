<template>
  <div class="column q-gutter-sm">
    <q-file
      v-model="picked"
      outlined
      dense
      label="Upload a file (max 10 MB)"
      :loading="uploading"
      accept=".pdf,.png,.jpg,.jpeg,.gif,.bmp,.webp,.svg,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.rtf,.md,.json,.xml,.zip"
      @update:model-value="onPick"
    >
      <template #prepend><q-icon name="o_attach_file" /></template>
    </q-file>

    <q-inner-loading :showing="loading && !attachments.length" />
    <div v-if="!loading && !attachments.length" class="text-grey-6 q-pa-md text-center">No attachments yet.</div>

    <q-list separator>
      <q-item v-for="a in attachments" :key="a.id">
        <q-item-section avatar>
          <q-icon :name="iconFor(a.fileExtension)" size="28px" color="primary" />
        </q-item-section>
        <q-item-section>
          <q-item-label class="ellipsis">{{ a.fileName }}</q-item-label>
          <q-item-label caption>
            {{ formatSize(a.fileSize) }} · {{ a.uploadedByName || "Unknown" }} · {{ formatDate(a.createdOnUtc) }}
          </q-item-label>
        </q-item-section>
        <q-item-section side>
          <div class="row">
            <q-btn flat round dense size="sm" icon="o_download" @click="download(a)" />
            <q-btn flat round dense size="sm" icon="o_delete" color="negative" @click="remove(a)" />
          </div>
        </q-item-section>
      </q-item>
    </q-list>
  </div>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { ufAttachmentsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true }
});

const MAX_BYTES = 10 * 1024 * 1024;

const notify = useNotify();
const { confirm } = useConfirm();
const { formatDate } = useDateFormat();

const attachments = ref([]);
const loading = ref(false);
const uploading = ref(false);
const picked = ref(null);

const load = async () => {
  loading.value = true;
  try {
    attachments.value = (await ufAttachmentsApi.list(props.entityType, props.entityId)) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const onPick = async (file) => {
  if (!file) return;
  if (file.size > MAX_BYTES) {
    notify.error("File exceeds the 10 MB limit.");
    picked.value = null;
    return;
  }
  uploading.value = true;
  try {
    await ufAttachmentsApi.upload(props.entityType, props.entityId, file);
    notify.success("Attachment uploaded.");
    picked.value = null;
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    uploading.value = false;
  }
};

const download = async (a) => {
  try {
    const blob = await ufAttachmentsApi.download(a.id);
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = a.fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const remove = async (a) => {
  const ok = await confirm({
    title: "Delete attachment",
    message: `Permanently delete "${a.fileName}"? This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await ufAttachmentsApi.remove(a.id);
    notify.success("Attachment deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const formatSize = (bytes) => {
  if (!bytes) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(i ? 1 : 0)} ${units[i]}`;
};

const iconFor = (ext) => {
  const e = (ext || "").toLowerCase();
  if ([".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg"].includes(e)) return "o_image";
  if (e === ".pdf") return "o_picture_as_pdf";
  if ([".xls", ".xlsx", ".csv"].includes(e)) return "o_table_chart";
  if ([".zip"].includes(e)) return "o_folder_zip";
  return "o_description";
};

onMounted(load);
defineExpose({ load });
</script>
