<template>
  <div>
    <div class="text-body2 text-grey-8 q-mb-sm">
      Add up to ten commission recipients and set each percentage (AC-REMS-016). Recipients become required
      approvers when the engagement is routed for approval.
    </div>

    <!-- Add recipient (searchable CSE-group picker; excludes those already added). -->
    <div v-if="editable" class="row items-end q-col-gutter-md q-mb-md">
      <app-select
        v-model="pick" :options="availableRecipients" label="Add recipient" class="col-12 col-sm"
        use-input :disable="splits.length >= 10" :hint="recipientHint"
        info="Lists users holding the &quot;CSE&quot; role, assigned on a user's page in Administration → Users. Recipients already added are excluded."
        @update:model-value="addRecipient"
      />
      <div class="col-auto text-caption text-grey-6 q-pb-sm">{{ splits.length }} / 10</div>
    </div>

    <div v-if="!splits.length" class="text-grey-6 q-pa-sm">No commission recipients yet.</div>

    <q-list v-else bordered separator class="rounded-borders">
      <q-item v-for="(s, i) in splits" :key="s.employeeId">
        <q-item-section>
          <!-- Truncated rather than allowed to set the row's width: the percentage box and the remove
               button are fixed, so on a narrow screen a long name would push them off the card. -->
          <q-item-label class="text-weight-medium ellipsis">{{ s.name }}</q-item-label>
        </q-item-section>
        <q-item-section side style="min-width: 120px;">
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
        <template v-if="totalOver"> — {{ overBy }}% over the 100% maximum</template>
      </div>
    </div>

    <!-- The allocation does not add up. Said as a banner rather than only as a caption, because a
         commission split that is short is the mistake that costs somebody money and it is invisible in a
         column of percentages that each look reasonable on their own.
         A WARNING, not a block: an engagement can legitimately sit part-allocated while the split is
         still being agreed, and this form saves itself as it is typed — refusing to save a total that
         reads 60% halfway through entering the second of three recipients would make the section
         unfillable. Over 100% is still refused outright, because that one is never right. -->
    <q-banner v-if="allocationWarning" dense class="rems-commission__warn q-mt-sm rounded-borders">
      <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
      {{ allocationWarning }}
    </q-banner>

  </div>
</template>

<script setup>
// The engagement commission splits (AC-REMS-016): up to ten recipients holding the CSE role, each with
// an editable percentage (> 0 and ≤ 100), individually removable.
//
// Controlled by the page: it holds the splits, announces every change (`change`), and the page's auto-save
// writes them. Its own "Save & Next" button was teleported into the workspace card's title row — a target
// that no longer exists, so the button rendered nowhere and the splits could not be saved at all.
import { ref, computed, watch, nextTick } from "vue";
import { remsApi } from "services/api";
import { useNotify } from "composables/useNotify";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";

const props = defineProps({
  engagement: { type: Object, required: true },
  // Selectable recipients — the holders of the "CSE" role, as [{ label, value }].
  recipientOptions: { type: Array, default: () => [] },
  editable: { type: Boolean, default: true }
});
// The page saves this section for the user, so every change to the splits is announced.
const emit = defineEmits(["change"]);
const notify = useNotify();

const buildSplits = (e) => (e.commissionSplits || []).map((s) => ({
  employeeId: s.employee.id,
  name: s.employee.name,
  percentage: s.percentage
}));
const splits = ref(buildSplits(props.engagement));
// Set while the splits are being re-seeded from a fresh engagement view — the server catching this
// component up, not a change to announce.
let syncing = false;
watch(() => props.engagement, (e) => {
  syncing = true;
  splits.value = buildSplits(e);
  nextTick(() => { syncing = false; });
});
// Deep: a percentage is edited in place on an existing row, which is not a new array.
watch(splits, () => { if (!syncing) emit("change"); }, { deep: true });

const pick = ref(null);
const availableRecipients = computed(() =>
  props.recipientOptions.filter((s) => !splits.value.some((x) => x.employeeId === s.value)));

// An empty picker means the group has no members; name it so the fix is obvious rather than the dropdown
// just being blank (mirrors the setup form's executive / billing-manager hints).
const recipientHint = computed(() => (props.recipientOptions.length
  ? ""
  : "Nobody holds the \"CSE\" role — assign it on a user's page in Administration → Users."));

const addRecipient = (value) => {
  if (!value || splits.value.length >= 10) { pick.value = null; return; }
  const option = props.recipientOptions.find((s) => s.value === value);
  if (option && !splits.value.some((x) => x.employeeId === value)) {
    splits.value.push({ employeeId: value, name: option.label, percentage: "" });
  }
  pick.value = null;
};

const removeAt = (i) => { splits.value.splice(i, 1); };

const percentRules = [
  (v) => (v !== "" && v !== null && Number(v) > 0 && Number(v) <= 100) || "Enter 0–100"
];

// Rounded to 2dp before comparing: three 33.33/33.34 splits sum to 100.00000000000001 in binary floating
// point, which would otherwise report a perfectly valid 100% allocation as over the limit.
const round2 = (n) => Math.round(n * 100) / 100;
const totalPercent = computed(() =>
  round2(splits.value.reduce((sum, s) => sum + (Number(s.percentage) || 0), 0)));
const totalOver = computed(() => totalPercent.value > 100);
const overBy = computed(() => round2(totalPercent.value - 100));

// Whether the allocation adds up, and what to say when it does not. Silent on an engagement with no
// recipients at all: naming nobody is how "there is no commission on this one" is recorded, and a warning
// there would fire on every engagement that never had a split.
const allocationWarning = computed(() => {
  if (!splits.value.length) return "";
  if (totalOver.value) {
    return `Commission totals ${totalPercent.value}% — that is ${overBy.value}% over. ` +
      "The splits divide one commission, so they cannot add up to more than the whole of it.";
  }
  if (totalPercent.value < 100) {
    return `Commission totals ${totalPercent.value}% — ${round2(100 - totalPercent.value)}% is unallocated. ` +
      "Check the split before this goes for approval.";
  }
  return "";
});

// Warn the moment the running total crosses 100, on the transition only — watching the flag rather than
// the total keeps this to one toast instead of one per keystroke. Save is blocked separately, since a
// toast is easy to miss. Only the OVER case gets a toast: an allocation still short of 100% is a
// half-finished split far more often than a mistake, and the banner is there to say so.
watch(totalOver, (over) => {
  if (over) {
    notify.warning(`Commission totals ${totalPercent.value}% — the total across all recipients cannot exceed 100%.`);
  }
});

// Called by the page's Save. Sent whenever the engagement has splits or had them a moment ago — an empty
// list is how a recipient is removed, so "nothing to send" is only true when there was nothing before.
const saveCommission = async (engagementId) => {
  const had = (props.engagement.commissionSplits || []).length > 0;
  if (!splits.value.length && !had) return null;

  // Every recipient must carry a valid percentage (> 0 and ≤ 100).
  const invalid = splits.value.some((s) => {
    const n = Number(s.percentage);
    return s.percentage === "" || s.percentage === null || !(n > 0 && n <= 100);
  });
  if (invalid) {
    throw new Error("Every commission recipient needs a percentage between 0 and 100.");
  }
  // The splits divide one commission, so they can never add up to more than the whole of it. The API
  // enforces the same ceiling — this is the readable version of that rejection.
  if (totalOver.value) {
    throw new Error(
      `Commission totals ${totalPercent.value}% — that is ${overBy.value}% over. Reduce the splits to 100% or less.`);
  }

  return remsApi.updateCommission(
    engagementId,
    splits.value.map((s) => ({ employeeId: s.employeeId, percentage: Number(s.percentage) }))
  );
};

defineExpose({ saveCommission });
</script>

<style scoped>
/* Amber, not red: the allocation not adding up is something to look at before the engagement is routed,
   not something that has failed. */
.rems-commission__warn {
  background: #fff8e1;
  color: #8a5a00;
}
</style>
