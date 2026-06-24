<template>
  <app-form-drawer
    v-model="open"
    :title="`Edit Template — ${template?.displayName || ''}`"
    save-label="Save"
    :saving="saving"
    @submit="onSubmit"
    @cancel="reset"
  >
    <q-form ref="formRef" greedy>
      <div v-if="template?.description" class="text-body2 text-grey-7 q-mb-md">{{ template.description }}</div>

      <app-text-field
        v-model="form.subject" label="Subject *" class="q-mb-md"
        :rules="[(v) => !!v || 'Subject is required']"
      />

      <app-text-field
        v-model="form.body" label="Body (HTML) *" type="textarea" autogrow class="q-mb-sm email-body"
        :rules="[(v) => !!v || 'Body is required']"
      />

      <!-- Placeholder helper: click to insert a token into the body. -->
      <div v-if="placeholders.length" class="q-mb-md">
        <div class="text-caption text-grey-7 q-mb-xs">Available placeholders (click to insert):</div>
        <q-chip
          v-for="p in placeholders" :key="p"
          clickable dense color="blue-1" text-color="primary" class="q-mr-xs q-mb-xs"
          @click="insertPlaceholder(p)"
        >
          {{ token(p) }}
        </q-chip>
      </div>

      <q-btn outline no-caps color="primary" icon="o_visibility" label="Preview" :loading="previewing" @click="onPreview" />
    </q-form>
  </app-form-drawer>

  <email-template-preview-dialog v-model="previewOpen" :subject="preview.subject" :body="preview.body" />
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { emailTemplateApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";

import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppTextField from "components/common/AppTextField.vue";
import EmailTemplatePreviewDialog from "modules/email-template/components/EmailTemplatePreviewDialog.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // The descriptor being edited: { key, displayName, description, subject, body, placeholders }.
  template: { type: Object, default: null },
  // Scope query params for the API: { tenantId } or { global: true }.
  scopeParams: { type: Object, default: () => ({}) }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

const form = reactive({ subject: "", body: "" });
const formRef = ref(null);
const saving = ref(false);
const placeholders = computed(() => props.template?.placeholders || []);

const reset = () => { form.subject = ""; form.body = ""; };

// Load the drawer from the descriptor each time it opens.
watch(() => props.modelValue, (isOpen) => {
  if (isOpen && props.template) {
    form.subject = props.template.subject || "";
    form.body = props.template.body || "";
  }
}, { immediate: true });

// Build a placeholder token (kept in JS so the literal braces don't confuse the template parser).
const token = (name) => `{{${name}}}`;
const insertPlaceholder = (name) => {
  form.body = `${form.body || ""}${token(name)}`;
};

const onSubmit = async () => {
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    await emailTemplateApi.save(props.template.key, { subject: form.subject, body: form.body }, props.scopeParams);
    notify.success("Template saved.");
    emit("saved");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

// ---- Preview ----
const previewOpen = ref(false);
const previewing = ref(false);
const preview = reactive({ subject: "", body: "" });

const onPreview = async () => {
  previewing.value = true;
  try {
    const rendered = await emailTemplateApi.preview(
      props.template.key, { subject: form.subject, body: form.body }, props.scopeParams);
    preview.subject = rendered?.subject || "";
    preview.body = rendered?.body || "";
    previewOpen.value = true;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    previewing.value = false;
  }
};

defineExpose({ form });
</script>

<style scoped>
.email-body :deep(textarea) {
  min-height: 220px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 13px;
}
</style>
