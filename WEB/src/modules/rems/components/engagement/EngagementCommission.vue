<template>
  <div>
    <div class="text-body2 text-grey-8 q-mb-sm">
      Add up to ten commission recipients and set each percentage (AC-REMS-016). Recipients become required
      approvers when the engagement is routed for approval.
    </div>

    <!-- Add recipient (searchable staff picker; excludes those already added). AppSelect does not forward
         use-input to its inner q-select, so a native q-select is used here to keep it searchable. -->
    <div v-if="editable" class="row items-end q-col-gutter-sm q-mb-md">
      <div class="app-field col-12 col-sm">
        <app-field-label label="Add recipient" />
        <q-select
          v-model="pick" :options="filteredStaff" outlined dense hide-bottom-space
          emit-value map-options option-value="value" option-label="label"
          use-input input-debounce="200" clearable :disable="splits.length >= 10"
          @filter="filterStaff" @update:model-value="addRecipient"
        >
          <template #no-option>
            <q-item><q-item-section class="text-grey-6">No matching staff</q-item-section></q-item>
          </template>
        </q-select>
      </div>
      <div class="col-auto text-caption text-grey-6 q-pb-sm">{{ splits.length }} / 10</div>
    </div>

    <div v-if="!splits.length" class="text-grey-6 q-pa-sm">No commission recipients yet.</div>

    <q-list v-else bordered separator class="rounded-borders">
      <q-item v-for="(s, i) in splits" :key="s.employeeId">
        <q-item-section>
          <q-item-label class="text-weight-medium">{{ s.name }}</q-item-label>
        </q-item-section>
        <q-item-section side style="min-width: 150px;">
          <app-text-field
            v-model="s.percentage" label="" type="number" :readonly="!editable" :rules="percentRules"
          >
            <template #append><span class="text-grey-7">%</span></template>
          </app-text-field>
        </q-item-section>
        <q-item-section side>
          <q-btn v-if="editable" flat round dense color="negative" icon="o_delete" @click="removeAt(i)">
            <q-tooltip>Remove</q-tooltip>
          </q-btn>
        </q-item-section>
      </q-item>
    </q-list>

    <div class="row items-center q-mt-md">
      <div class="text-caption" :class="totalOver ? 'text-negative' : 'text-grey-7'">
        Total allocated: {{ totalPercent }}%
      </div>
      <q-space />
      <q-btn
        v-if="editable" unelevated no-caps color="primary" icon="o_save" label="Save commission"
        :loading="saving" @click="save"
      />
    </div>
  </div>
</template>

<script setup>
// The engagement commission splits (AC-REMS-016): up to ten recipients from the staff list, each with an
// editable percentage (> 0 and ≤ 100), individually removable.
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import AppFieldLabel from "components/common/AppFieldLabel.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  // The staff list: [{ label, value }].
  staff: { type: Array, default: () => [] },
  editable: { type: Boolean, default: true }
});
const emit = defineEmits(["saved"]);

const notify = useNotify();

const buildSplits = (e) => (e.commissionSplits || []).map((s) => ({
  employeeId: s.employee.id,
  name: s.employee.name,
  percentage: s.percentage
}));
const splits = ref(buildSplits(props.engagement));
watch(() => props.engagement, (e) => { splits.value = buildSplits(e); });

const pick = ref(null);
const availableStaff = computed(() =>
  props.staff.filter((s) => !splits.value.some((x) => x.employeeId === s.value)));

// Searchable staff filter for the native q-select picker.
const staffFilter = ref("");
const filteredStaff = computed(() => {
  const needle = staffFilter.value.toLowerCase();
  return availableStaff.value.filter((s) => !needle || s.label.toLowerCase().includes(needle));
});
const filterStaff = (val, update) => { update(() => { staffFilter.value = val || ""; }); };

const addRecipient = (value) => {
  if (!value || splits.value.length >= 10) { pick.value = null; return; }
  const staffOpt = props.staff.find((s) => s.value === value);
  if (staffOpt && !splits.value.some((x) => x.employeeId === value)) {
    splits.value.push({ employeeId: value, name: staffOpt.label, percentage: "" });
  }
  pick.value = null;
};

const removeAt = (i) => { splits.value.splice(i, 1); };

const percentRules = [
  (v) => (v !== "" && v !== null && Number(v) > 0 && Number(v) <= 100) || "Enter 0–100"
];

const totalPercent = computed(() =>
  splits.value.reduce((sum, s) => sum + (Number(s.percentage) || 0), 0));
const totalOver = computed(() => totalPercent.value > 100);

const saving = ref(false);
const save = async () => {
  // Every recipient must carry a valid percentage (> 0 and ≤ 100).
  const invalid = splits.value.some((s) => {
    const n = Number(s.percentage);
    return s.percentage === "" || s.percentage === null || !(n > 0 && n <= 100);
  });
  if (invalid) {
    notify.warning("Every recipient needs a percentage between 0 and 100.");
    return;
  }
  saving.value = true;
  try {
    const view = await remsApi.updateCommission(
      props.engagement.id,
      splits.value.map((s) => ({ employeeId: s.employeeId, percentage: Number(s.percentage) }))
    );
    emit("saved", view);
    notify.success("Commission splits saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>
