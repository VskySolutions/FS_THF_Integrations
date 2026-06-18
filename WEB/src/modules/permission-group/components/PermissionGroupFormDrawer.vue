<template>
  <app-form-drawer
    ref="drawerRef"
    v-model="open"
    :title="groupId ? 'Edit Permission Group' : 'Create Permission Group'"
    save-label="Save"
    :saving="saving"
    draft-key="permission-group-form"
    :draft="draftView"
    @submit="onSubmit"
    @cancel="resetForm"
    @restore-draft="restoreDraft"
  >
    <q-form ref="formRef" greedy>
      <app-select
        v-if="canChooseTenant" v-model="form.tenantId" :options="tenantOptions" label="Tenant *"
        :loading="loadingTenants" :clearable="false" class="q-mb-md" :readonly="!!groupId"
        :rules="[(v) => !!v || 'Tenant is required']"
      />

      <app-text-field
        v-model="form.name" label="Name *" class="q-mb-md"
        :rules="[(v) => !!v || 'Name is required']"
      />
      <app-text-field v-model="form.description" label="Description" type="textarea" autogrow class="q-mb-md" />

      <!-- Permission keys: grouped by category, live-searchable, with a real-time count badge. -->
      <div class="row items-center q-mb-sm">
        <div class="section-subhead">Permission Keys</div>
        <q-space />
        <q-badge color="primary" class="count-badge" data-test="key-count">{{ selectedKeys.length }} selected</q-badge>
      </div>

      <q-input
        v-model="keySearch" outlined dense clearable label="Filter keys" class="q-mb-sm"
      >
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>

      <div v-if="loadingCatalog" class="row flex-center q-pa-md"><q-spinner color="primary" size="28px" /></div>

      <q-list v-else bordered class="rounded-borders">
        <q-expansion-item
          v-for="group in visibleCategories"
          :key="group.category"
          :label="group.category"
          :caption="`${countSelectedIn(group.keys)} / ${group.keys.length} selected`"
          default-opened
          dense
          header-class="text-primary text-weight-medium"
        >
          <q-list dense>
            <q-item v-for="key in group.keys" :key="key" tag="label" :disable="isDisabled(key)">
              <q-item-section side top>
                <q-checkbox v-model="selectedKeys" :val="key" :disable="isDisabled(key)" dense />
              </q-item-section>
              <q-item-section>
                <q-item-label :class="{ 'text-grey-5': isDisabled(key) }">
                  {{ humanizeKey(key) }}
                  <q-tooltip>{{ key }}</q-tooltip>
                </q-item-label>
              </q-item-section>
              <q-item-section v-if="isDisabled(key)" side>
                <q-icon name="o_lock" color="grey-5" size="16px">
                  <q-tooltip>Not available in your tenant</q-tooltip>
                </q-icon>
              </q-item-section>
            </q-item>
          </q-list>
        </q-expansion-item>
        <q-item v-if="!visibleCategories.length">
          <q-item-section class="text-grey-6">No permission keys match your search.</q-item-section>
        </q-item>
      </q-list>
    </q-form>
  </app-form-drawer>
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { permissionGroupApi, roleApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useTenantOptions } from "composables/useTenantOptions";
import { useNotify } from "composables/useNotify";
import {
  humanizeKey, groupKeysByCategory, categoryForKey, ELEVATED_KEYS
} from "composables/usePermissionCategories";

import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // Optional pre-selected tenant (super admin's chosen list scope).
  tenantId: { type: String, default: null },
  // When set, the drawer edits the existing group; otherwise it creates a new one.
  groupId: { type: String, default: null },
  // Optional template to pre-populate from on open: { name, description, permissionKeys[] }.
  template: { type: Object, default: null }
});
const emit = defineEmits(["update:modelValue", "saved"]);

const notify = useNotify();
const { canChooseTenant, activeTenantId, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const blankForm = () => ({ tenantId: null, name: "", description: "" });
const form = reactive(blankForm());
const selectedKeys = ref([]);
const formRef = ref(null);
const drawerRef = ref(null);
const saving = ref(false);

// ---- Permission catalogue ----
const catalog = ref([]);
const loadingCatalog = ref(false);
const keySearch = ref("");

const loadCatalog = async () => {
  if (catalog.value.length) return;
  loadingCatalog.value = true;
  try {
    catalog.value = await permissionGroupApi.permissionCatalog() || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingCatalog.value = false;
  }
};

// Categorised + filtered catalogue for rendering.
const categorized = computed(() => groupKeysByCategory(catalog.value));
const visibleCategories = computed(() => {
  const needle = (keySearch.value || "").toLowerCase().trim();
  if (!needle) return categorized.value;
  return categorized.value
    .map((g) => ({
      ...g,
      keys: g.keys.filter((k) => k.toLowerCase().includes(needle) || humanizeKey(k).toLowerCase().includes(needle))
    }))
    .filter((g) => g.keys.length);
});

const countSelectedIn = (keys) => keys.filter((k) => selectedKeys.value.includes(k)).length;

// ---- Tenant ceiling (best-effort, never blocks submit) ----
// Super Admins see no ceiling. For Tenant Admins, prefer the union of keys across the tenant's
// roles; fall back to the catalogue minus known elevated keys.
const ceiling = ref(null); // null → no restriction (super admin)

const computeCeiling = async () => {
  if (canChooseTenant.value) { ceiling.value = null; return; }
  try {
    const roles = await roleApi.list({});
    const union = new Set();
    for (const r of roles || []) {
      const full = await roleApi.get(r.id);
      (full?.permissions || []).forEach((k) => union.add(k));
      (full?.effectivePermissions || []).forEach((k) => union.add(k));
    }
    ceiling.value = union.size ? union : new Set(catalog.value.filter((k) => !ELEVATED_KEYS.includes(k)));
  } catch {
    // best-effort fallback
    ceiling.value = new Set(catalog.value.filter((k) => !ELEVATED_KEYS.includes(k)));
  }
};

const isDisabled = (key) => ceiling.value != null && !ceiling.value.has(key);

const resetForm = () => {
  Object.assign(form, blankForm());
  selectedKeys.value = [];
  keySearch.value = "";
};

const restoreDraft = (saved) => {
  Object.assign(form, blankForm(), saved);
  if (Array.isArray(saved?.permissionKeys)) selectedKeys.value = [...saved.permissionKeys];
};

const applyTemplate = (template) => {
  if (!template) return;
  form.name = template.name || "";
  form.description = template.description || "";
  selectedKeys.value = [...(template.permissionKeys || [])];
};

const loadGroup = async (id) => {
  try {
    const g = await permissionGroupApi.get(id);
    form.tenantId = g.tenantId;
    form.name = g.name;
    form.description = g.description || "";
    selectedKeys.value = [...(g.permissionKeys || [])];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// Prepare the form whenever the drawer opens.
watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen) return;
  resetForm();
  await loadCatalog();
  if (canChooseTenant.value) {
    await loadTenants();
    form.tenantId = props.tenantId || activeTenantId.value;
  }
  await computeCeiling();
  if (props.groupId) {
    await loadGroup(props.groupId);
  } else if (props.template) {
    applyTemplate(props.template);
  }
}, { immediate: true });

// The auto-saved draft snapshot includes the selected keys so they survive a tenant-switch guard.
const draftView = computed(() => ({ ...form, permissionKeys: selectedKeys.value }));

const onSubmit = async ({ clearDraft } = {}) => {
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    const payload = {
      name: form.name,
      description: form.description || null,
      permissionKeys: selectedKeys.value
    };
    if (props.groupId) {
      await permissionGroupApi.update(props.groupId, payload);
    } else {
      if (canChooseTenant.value && form.tenantId) payload.tenantId = form.tenantId;
      await permissionGroupApi.create(payload);
    }
    clearDraft?.();
    resetForm();
    notify.success("Permission group saved.");
    emit("saved");
  } catch (err) {
    const code = getApiErrorCode(err);
    if (code === ApiErrorCodes.PermissionCeilingExceeded) {
      notify.error(getApiErrorMessage(err, "One or more keys are outside your tenant's permission ceiling."));
    } else if (code === ApiErrorCodes.DuplicateGroupName) {
      notify.error("A group with that name already exists.");
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    saving.value = false;
  }
};

// Exposed for the parent list/detail (and unit tests).
defineExpose({ form, selectedKeys, visibleCategories, categoryForKey });
</script>

<style scoped>
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
}
.count-badge {
  font-size: 12px;
}
</style>
