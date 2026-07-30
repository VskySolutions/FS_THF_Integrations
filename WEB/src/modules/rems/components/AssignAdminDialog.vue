<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 420px; max-width: 92vw;">
      <q-card-section class="text-h6">{{ isPickup ? "Pick Up Request" : "Assign Admin" }}</q-card-section>
      <q-separator />
      <q-card-section>
        <div class="text-body2 text-grey-7 q-mb-md">
          {{ isPickup
            ? "Assign this request to yourself and start working it."
            : "Choose the Admin to own this request. Reassigning notifies the new owner." }}
        </div>
        <app-select
          v-model="adminId" :options="adminOptions" label="Admin" required
          :loading="loading" :clearable="false"
        />
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat no-caps color="grey-8" label="Cancel" @click="open = false" />
        <q-btn
          unelevated no-caps color="primary" :label="confirmLabel"
          :loading="saving" :disable="!adminId || saving" @click="confirm"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useAuthStore } from "stores/auth";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  requestId: { type: String, default: null },
  // The current assignee id, pre-selected in reassignment mode.
  currentAdminId: { type: String, default: null },
  // "pickup" pre-selects the current user (assign to self); "assign" pre-selects the current owner.
  mode: { type: String, default: "assign" }
});
const emit = defineEmits(["update:modelValue", "assigned"]);

const notify = useNotify();
const auth = useAuthStore();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const isPickup = computed(() => props.mode === "pickup");
const confirmLabel = computed(() => {
  if (isPickup.value) return "Pick Up";
  return props.currentAdminId ? "Reassign" : "Assign";
});

const adminOptions = ref([]);
const adminId = ref(null);
const loading = ref(false);
const saving = ref(false);

const loadAdmins = async () => {
  loading.value = true;
  try {
    const admins = await remsApi.admins();
    let options = (admins || []).map((a) => ({ label: a.name, value: a.id }));
    // Pick-up assigns to self; ensure the current user is selectable even if not in the Admin list.
    if (isPickup.value && auth.user?.userId && !options.some((o) => o.value === auth.user.userId)) {
      options = [{ label: auth.user.displayName || "Me", value: auth.user.userId }, ...options];
    }
    adminOptions.value = options;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    adminOptions.value = [];
  } finally {
    loading.value = false;
  }
};

watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen) return;
  adminId.value = isPickup.value ? (auth.user?.userId || null) : (props.currentAdminId || null);
  await loadAdmins();
});

const confirm = async () => {
  if (!adminId.value || !props.requestId) return;
  saving.value = true;
  try {
    const detail = await remsApi.assign(props.requestId, adminId.value);
    notify.success(isPickup.value ? "Request picked up." : "Request assigned.");
    emit("assigned", detail);
    open.value = false;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>
