<template>
  <div class="column q-gutter-sm">
    <div class="row justify-end">
      <q-btn dense no-caps unelevated color="primary" icon="o_add" label="Add checklist" @click="openCreate" />
    </div>

    <q-inner-loading :showing="loading && !checklists.length" />
    <div v-if="!loading && !checklists.length" class="text-grey-6 q-pa-md text-center">No checklists yet.</div>

    <q-card v-for="cl in checklists" :key="cl.id" flat bordered class="q-pa-sm">
      <div class="row items-center justify-between">
        <div class="text-weight-medium" :class="{ 'text-strike text-grey-6': cl.completedCount === cl.totalCount && cl.totalCount > 0 }">
          {{ cl.title }}
        </div>
        <div class="row items-center q-gutter-xs">
          <span class="fs-12 text-grey-7">{{ cl.completedCount }}/{{ cl.totalCount }}</span>
          <q-btn flat round dense size="sm" icon="o_delete" color="negative" @click="removeChecklist(cl)" />
        </div>
      </div>
      <q-linear-progress
        :value="cl.totalCount ? cl.completedCount / cl.totalCount : 0"
        color="positive"
        track-color="grey-3"
        size="6px"
        rounded
        class="q-my-xs"
      />

      <q-list dense>
        <q-item v-for="item in cl.items" :key="item.id" class="q-px-none">
          <q-item-section avatar>
            <q-checkbox
              :model-value="item.isCompleted"
              dense
              @update:model-value="(val) => toggle(cl, item, val)"
            />
          </q-item-section>
          <q-item-section :class="{ 'text-strike text-grey-6': item.isCompleted }">{{ item.text }}</q-item-section>
          <q-item-section side>
            <q-btn flat round dense size="sm" icon="o_close" @click="removeItem(cl, item)" />
          </q-item-section>
        </q-item>
      </q-list>

      <div class="row q-gutter-xs q-mt-xs">
        <q-input
          v-model="newItemText[cl.id]"
          dense
          outlined
          placeholder="Add item"
          class="col"
          @keyup.enter="addItem(cl)"
        />
        <q-btn flat dense no-caps color="primary" label="Add" :disable="!newItemText[cl.id]" @click="addItem(cl)" />
      </div>
    </q-card>

    <!-- Create checklist drawer -->
    <app-form-drawer v-model="createOpen" title="Add checklist" :saving="saving" @submit="create" @cancel="createOpen = false">
      <q-form ref="formRef">
        <app-text-field v-model="form.title" label="Title *" :rules="[(v) => !!v || 'Title is required']" />
        <app-text-field
          v-model="form.itemsText"
          label="Items (one per line)"
          type="textarea"
          autogrow
          hint="Optional — add initial items, one per line"
        />
      </q-form>
    </app-form-drawer>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from "vue";
import { ufChecklistApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  entityType: { type: Number, required: true },
  entityId: { type: String, required: true }
});

const notify = useNotify();
const { confirm } = useConfirm();

const checklists = ref([]);
const loading = ref(false);
const newItemText = reactive({});

const createOpen = ref(false);
const saving = ref(false);
const formRef = ref(null);
const form = reactive({ title: "", itemsText: "" });

const load = async () => {
  loading.value = true;
  try {
    checklists.value = (await ufChecklistApi.list(props.entityType, props.entityId)) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const openCreate = () => {
  form.title = "";
  form.itemsText = "";
  createOpen.value = true;
};

const create = async () => {
  const valid = await formRef.value?.validate();
  if (valid === false) return;
  saving.value = true;
  try {
    const items = (form.itemsText || "").split("\n").map((s) => s.trim()).filter(Boolean);
    await ufChecklistApi.create({
      entityType: props.entityType,
      entityId: props.entityId,
      title: form.title.trim(),
      items
    });
    createOpen.value = false;
    notify.success("Checklist created.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

const toggle = async (cl, item, val) => {
  try {
    const updated = await ufChecklistApi.toggleItem(cl.id, item.id, val);
    replaceChecklist(updated);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const addItem = async (cl) => {
  const text = (newItemText[cl.id] || "").trim();
  if (!text) return;
  try {
    const updated = await ufChecklistApi.addItem(cl.id, text);
    newItemText[cl.id] = "";
    replaceChecklist(updated);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const removeItem = async (cl, item) => {
  try {
    await ufChecklistApi.removeItem(cl.id, item.id);
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const removeChecklist = async (cl) => {
  const ok = await confirm({
    title: "Delete checklist",
    message: `Delete "${cl.title}" and all its items?`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await ufChecklistApi.remove(cl.id);
    notify.success("Checklist deleted.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const replaceChecklist = (updated) => {
  if (!updated) return load();
  const idx = checklists.value.findIndex((c) => c.id === updated.id);
  if (idx >= 0) checklists.value.splice(idx, 1, updated);
};

onMounted(load);
defineExpose({ load });
</script>
