<template>
  <div>
    <div class="text-body2 text-grey-8 q-mb-sm">
      Add up to ten commission recipients and set each percentage (AC-REMS-016). Recipients become required
      approvers when the engagement is routed for approval.
    </div>

    <!-- Add recipient (searchable staff picker; excludes those already added). -->
    <div v-if="editable" class="row items-end q-col-gutter-md q-mb-md">
      <app-select
        v-model="pick" :options="availableStaff" label="Add recipient" class="col-12 col-sm"
        use-input :disable="splits.length >= 10"
        @update:model-value="addRecipient"
      />
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
    </div>

    <!-- Into the card's title row, alongside every other tab's primary save. `defer` because the
         target is rendered by the same tree (see EngagementSetupForm). -->
    <teleport v-if="isVisibleTab && editable" defer to="#engagement-header-actions">
      <q-btn
        unelevated no-caps color="primary" icon-right="o_arrow_forward" label="Save & Next"
        :loading="saving" @click="save"
      />
    </teleport>
  </div>
</template>

<script setup>
// The engagement commission splits (AC-REMS-016): up to ten recipients from the staff list, each with an
// editable percentage (> 0 and ≤ 100), individually removable.
import { ref, computed, watch } from "vue";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useVisibleTab } from "composables/useVisibleTab";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  // The staff list: [{ label, value }].
  staff: { type: Array, default: () => [] },
  editable: { type: Boolean, default: true }
});
const emit = defineEmits(["saved", "advance"]);

const notify = useNotify();
const { isVisibleTab } = useVisibleTab();

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
    emit("advance");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};
</script>
