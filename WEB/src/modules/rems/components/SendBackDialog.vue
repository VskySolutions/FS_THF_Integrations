<template>
  <q-dialog v-model="open">
    <q-card class="sbd">
      <q-card-section class="row items-center no-wrap">
        <div class="col">
          <div class="text-subtitle1 text-primary">Send back to initiator</div>
          <div v-if="remsNumber" class="text-caption text-grey-7">{{ remsNumber }}</div>
        </div>
        <q-btn flat round dense icon="o_close" color="grey-7" @click="open = false" />
      </q-card-section>
      <q-separator />

      <q-card-section>
        <div class="text-body2 q-mb-md">
          They will be able to change the <strong>Engagement Setup</strong> only — the client's own
          answers stay read-only to them. They hand it back to you to confirm before it can go for
          approval.
        </div>
        <app-text-field
          v-model="reason" label="What needs changing?" type="textarea" autogrow required autofocus
          placeholder="Be specific — this is the whole instruction they get."
          :error="attempted && !reason.trim()"
          error-message="A reason is required. A return with no reason is not actionable."
        />
      </q-card-section>

      <q-separator />
      <q-card-actions align="right">
        <q-btn flat no-caps color="grey-8" label="Cancel" @click="open = false" />
        <q-btn unelevated no-caps color="orange-9" label="Send back" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
// The admin's return of a request for engagement-setup rework. The reason is mandatory on the server too;
// asking for it here is so they are not told off after the fact.
import { ref, computed, watch } from "vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  remsNumber: { type: String, default: "" }
});
const emit = defineEmits(["update:modelValue", "confirm"]);

const reason = ref("");
const attempted = ref(false);

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

watch(open, (isOpen) => {
  if (isOpen) {
    reason.value = "";
    attempted.value = false;
  }
});

const confirm = () => {
  attempted.value = true;
  if (!reason.value.trim()) return;
  emit("confirm", reason.value.trim());
};
</script>

<style scoped>
.sbd {
  width: 520px;
  max-width: 92vw;
  border-radius: 12px;
}
</style>
