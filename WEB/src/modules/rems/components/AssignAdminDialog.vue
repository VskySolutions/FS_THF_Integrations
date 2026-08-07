<template>
  <q-dialog v-model="open">
    <q-card style="min-width: 420px; max-width: 92vw;">
      <q-card-section class="text-h6">{{ title }}</q-card-section>
      <q-separator />
      <q-card-section>
        <!-- Which request this is about. The action is reached from a row in a list of near-identical
             titles, so naming it here is the difference between confident and hoping. -->
        <div v-if="requestNumber || requestTitle" class="assign-target q-mb-md">
          <div class="assign-target__number">{{ requestNumber }}</div>
          <div class="assign-target__title">{{ requestTitle }}</div>
        </div>
        <div class="text-body2 text-grey-7 q-mb-md">{{ explanation }}</div>
        <app-select
          v-model="adminId" :options="adminOptions" label="Admin" required
          :loading="loading" :clearable="false"
          info="Lists users holding the Admin or Super Admin role in this tenant. You are always listed, so a request can be picked up by yourself."
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
import AppSelect from "components/common/AppSelect.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  requestId: { type: String, default: null },
  // The current assignee id, pre-selected in reassignment mode.
  currentAdminId: { type: String, default: null },
  // "pickup" pre-selects the current user (assign to self); "assign" pre-selects the current owner;
  // "draft" hands an unsubmitted draft to a named Admin, which also submits it (see isDraft below).
  mode: { type: String, default: "assign" },
  // Names the request being assigned, shown above the picker.
  requestNumber: { type: String, default: "" },
  requestTitle: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue", "assigned"]);

const notify = useNotify();
const auth = useAuthStore();

const open = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val)
});

const isPickup = computed(() => props.mode === "pickup");

// A draft is creator-only server-side — not even an Admin can open one. Assigning it therefore has to
// submit it in the same step, or the new owner would be notified about a request they cannot see.
const isDraft = computed(() => props.mode === "draft");

// An unclaimed request opens pre-selected to the caller, but the dropdown is a free choice of any Admin.
// The title/button follow the CURRENT selection so the dialog stops promising a self-assignment the
// moment someone else is chosen — the old copy said "assign to yourself" over an editable picker.
const isSelf = computed(() => !!adminId.value && adminId.value === auth.user?.userId);
const title = computed(() => {
  if (isDraft.value) return "Assign to Admin";
  return isPickup.value && isSelf.value ? "Pick Up Request" : "Assign Admin";
});
const explanation = computed(() => {
  if (isDraft.value) {
    return "This draft is visible only to you. Assigning it submits the request to the Admin you " +
      "choose, who is notified and becomes its owner.";
  }
  return isPickup.value
    ? "Take this yourself, or pick another Admin to own it. The new owner is notified."
    : "Choose the Admin to own this request. Reassigning notifies the new owner.";
});
const confirmLabel = computed(() => {
  if (isDraft.value) return "Submit & Assign";
  if (isPickup.value) return isSelf.value ? "Pick Up" : "Assign";
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
    // Submitting and assigning travel together on the edit endpoint, so a draft handed to an Admin
    // becomes theirs and becomes visible in one transaction rather than two half-states.
    const detail = isDraft.value
      ? await remsApi.update(props.requestId, { assignAdminUserId: adminId.value, submit: true })
      : await remsApi.assign(props.requestId, adminId.value);
    if (isDraft.value) notify.success("Draft submitted and assigned.");
    else notify.success(isPickup.value && isSelf.value ? "Request picked up." : "Request assigned.");
    emit("assigned", detail);
    open.value = false;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>

<style scoped>
.assign-target {
  padding: 8px 12px;
  border: 1px solid var(--line);
  border-left: 3px solid var(--q-primary);
  border-radius: 6px;
  background: var(--teal-050);
}
.assign-target__number {
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.04em;
  color: var(--teal-700);
}
.assign-target__title {
  font-size: 14px;
  color: var(--ink-900);
  word-break: break-word;
}
</style>
