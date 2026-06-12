<template>
  <q-drawer
    v-model="open"
    side="right"
    overlay
    bordered
    :width="currentWidth"
    class="app-form-drawer column no-wrap"
  >
    <!-- Drag handle: resize the drawer; the chosen width persists until logout. -->
    <div class="app-form-drawer__resizer" @mousedown="startResize" @dblclick="resetWidth" />

    <!-- Fixed header -->
    <div class="row items-center q-pa-md bg-primary text-white">
      <div class="text-h6">{{ title }}</div>
      <q-space />
      <q-btn flat round dense color="white" icon="o_close" @click="onCancel" />
    </div>
    <q-separator />

    <!-- Scrollable body -->
    <q-scroll-area class="col">
      <div class="q-pa-md">
        <slot />
      </div>
    </q-scroll-area>

    <!-- Fixed footer -->
    <q-separator />
    <div class="row justify-end q-gutter-sm q-pa-md bg-grey-1">
      <q-btn flat no-caps color="grey-8" label="Cancel" @click="onCancel" />
      <q-btn
        unelevated
        no-caps
        color="primary"
        :label="saveLabel"
        :loading="saving"
        :disable="saving"
        @click="onSubmit"
      />
    </div>
  </q-drawer>
</template>

<script setup>
import { computed, ref, watch, onBeforeUnmount } from "vue";
import { LocalStorage } from "quasar";
import { useTenantStore } from "stores/tenant";
import { usePreferences } from "composables/usePreferences";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: "" },
  saveLabel: { type: String, default: "Save" },
  saving: { type: Boolean, default: false },
  // When set, the form's draft is auto-persisted under this key.
  draftKey: { type: String, default: "" },
  draft: { type: Object, default: null }
});

const emit = defineEmits(["update:modelValue", "submit", "cancel", "restore-draft"]);

const tenantStore = useTenantStore();
const prefs = props.draftKey ? usePreferences(props.draftKey) : null;

// ---- Resizable width ----
// The user can drag the left edge to resize. The width is shared across all drawers and stored
// in LocalStorage, which auth.clearSession() wipes on logout — so it persists until the user logs out.
// Sizes are viewport-relative: 50% by default, never below 30%.
const WIDTH_KEY = "appDrawerWidth";
const viewport = () => (typeof window !== "undefined" ? window.innerWidth : 1200);
const minWidth = () => Math.round(viewport() * 0.30);
const maxWidth = () => Math.round(viewport() * 0.95);
const defaultWidth = () => Math.round(viewport() * 0.50);
const clampWidth = (w) => Math.min(maxWidth(), Math.max(minWidth(), w));

const storedWidth = Number(LocalStorage.getItem(WIDTH_KEY));
const currentWidth = ref(clampWidth(storedWidth > 0 ? storedWidth : defaultWidth()));

let startX = 0;
let startWidth = 0;

const onResizeMove = (e) => {
  // Right-side drawer grows as the pointer moves left.
  currentWidth.value = clampWidth(startWidth + (startX - e.clientX));
};

const stopResize = () => {
  document.removeEventListener("mousemove", onResizeMove);
  document.removeEventListener("mouseup", stopResize);
  document.body.style.userSelect = "";
  LocalStorage.set(WIDTH_KEY, currentWidth.value);
};

const startResize = (e) => {
  startX = e.clientX;
  startWidth = currentWidth.value;
  document.body.style.userSelect = "none";
  document.addEventListener("mousemove", onResizeMove);
  document.addEventListener("mouseup", stopResize);
};

const resetWidth = () => {
  currentWidth.value = defaultWidth();
  LocalStorage.set(WIDTH_KEY, currentWidth.value);
};

onBeforeUnmount(stopResize);

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

let draftTimer = null;

// Auto-save the draft (debounced 2s) and flag unsaved state for the tenant-switch guard.
watch(() => props.draft, (value) => {
  if (!prefs || !props.modelValue || value == null) {
    return;
  }
  tenantStore.setUnsavedForm(true);
  clearTimeout(draftTimer);
  draftTimer = setTimeout(() => prefs.set("formDraft", value), 2000);
}, { deep: true });

// Restore any saved draft when the drawer opens.
watch(() => props.modelValue, (isOpen) => {
  if (isOpen && prefs) {
    const saved = prefs.get("formDraft", null);
    if (saved) {
      emit("restore-draft", saved);
    }
  } else if (!isOpen) {
    tenantStore.setUnsavedForm(false);
  }
});

const clearDraft = () => {
  clearTimeout(draftTimer);
  if (prefs) {
    prefs.remove("formDraft");
  }
  tenantStore.setUnsavedForm(false);
};

const onSubmit = () => {
  if (props.saving) {
    return; // double-click prevention
  }
  emit("submit", { clearDraft });
};

const onCancel = () => {
  clearDraft();
  emit("cancel");
  open.value = false;
};

defineExpose({ clearDraft });
</script>

<style scoped>
.app-form-drawer {
  height: 100%;
}
.app-form-drawer__resizer {
  position: absolute;
  top: 0;
  left: 0;
  width: 6px;
  height: 100%;
  cursor: ew-resize;
  z-index: 10;
  background: transparent;
  transition: background 0.15s ease;
}
.app-form-drawer__resizer:hover {
  background: var(--q-primary);
  opacity: 0.4;
}
</style>
