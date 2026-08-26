<template>
  <!-- Nothing at all when a caller excluding its own round has no OTHER round to show: an empty "Earlier
       rounds" panel on a first-round engagement is a drawer opened onto nothing. The unfiltered use keeps
       its panel and says why it is empty. -->
  <q-expansion-item v-if="!collapsed" icon="o_history" :label="label" dense-toggle class="ah">
    <div v-if="loading" class="q-pa-md row flex-center"><q-spinner color="primary" size="24px" /></div>
    <div v-else-if="failed" class="q-pa-md text-grey-6">
      This engagement's approval history is not available to you.
    </div>
    <div v-else-if="!shownRounds.length" class="q-pa-md text-grey-6">
      This engagement has not been routed for approval yet.
    </div>
    <div v-else class="q-pa-sm column q-gutter-sm">
      <q-card v-for="r in shownRounds" :key="r.roundId" flat bordered class="ah__round">
        <q-card-section class="ah__head row items-center q-py-sm">
          <div class="col">
            <span class="text-weight-medium">Round {{ r.roundNumber }}</span>
            <span class="text-grey-7"> · sent by {{ r.sentBy }} on {{ fmt.formatDateTime(r.sentOnUtc) }}</span>
          </div>
          <!-- The decline count against what it would have taken: a round that carried with one objection
               against it reads very differently from a unanimous one. -->
          <q-badge :color="roundColor(r.status)">
            {{ roundLabel(r) }}
          </q-badge>
        </q-card-section>
        <q-separator />
        <!-- Server order: by role — shareholder, director, CSE, commission recipient, then anyone added by
             hand — the same sequence the Approval tab and the round list read in. -->
        <q-list dense separator>
          <q-item
            v-for="d in r.decisions" :key="d.taskId"
            :class="{ 'ah--awaiting': awaitingDecision(r, d) }"
          >
            <q-item-section>
              <q-item-label>
                {{ d.approver }}
                <span class="text-grey-6">· {{ approverRoleLabel(d.role) }}</span>
              </q-item-label>
              <q-item-label v-if="d.reason" caption class="text-red-9">{{ d.reason }}</q-item-label>
              <q-item-label v-if="d.checklistTotal" caption>
                Checklist {{ d.checklistCompleted }} / {{ d.checklistTotal }}
              </q-item-label>
            </q-item-section>
            <q-item-section side>
              <div class="column items-end">
                <q-badge :color="decisionColor(d.status)">{{ decisionLabel(d.status) }}</q-badge>
                <span v-if="d.decidedOnUtc" class="text-caption text-grey-6 q-mt-xs">
                  {{ fmt.formatDateTime(d.decidedOnUtc) }}
                </span>
              </div>
            </q-item-section>
          </q-item>
        </q-list>
      </q-card>
    </div>
  </q-expansion-item>
</template>

<script setup>
// Every approval round on an engagement, newest first — Round 3, Round 2, Round 1. Rounds are immutable
// and numbered from 1 — a resubmission creates a new one rather than resetting the last — so this is the
// whole record of what the approvers did, including rounds that failed. The order is the server's; this
// renders what it returns rather than sorting again.
import { ref, computed, watch } from "vue";
import { remsApi } from "services/api";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";

const props = defineProps({
  engagementId: { type: String, default: null },
  // A round the CALLER already renders in full, left out here rather than repeated directly beneath
  // itself. It is the round's id and not "the newest", because the two are not always the same one: an
  // approver reaching a historical task out of their inbox is shown THAT round, while a later one may
  // already be open — and dropping the newest would then hide a round nothing else on the page shows,
  // while still repeating the one it does.
  excludeRoundId: { type: String, default: null },
  // Say what the panel holds. A caller excluding its own round is showing the rounds BEFORE it, and
  // "Approval history" over a list quietly missing a round claims more than it delivers.
  label: { type: String, default: "Approval history" }
});

const fmt = useDateFormat();
// The role as the rest of REMS words it. The raw enum name reached this panel alone — "DepartmentDirector"
// beside "Department Director" everywhere else — and this is a list read for its ROLE sequence.
const { approverRoleLabel } = useRemsMeta();
const rounds = ref([]);
const loading = ref(false);
// Told apart from an empty history on purpose. This panel also sits on the approver's own task now,
// and reading the engagement behind it is not a permission every approver holds — a 403 rendered as
// "not routed for approval yet" would be a flat lie to somebody staring at an open round.
const failed = ref(false);

const shownRounds = computed(() =>
  (props.excludeRoundId
    ? rounds.value.filter((r) => r.roundId !== props.excludeRoundId)
    : rounds.value));

// A panel with an exclusion and nothing left over has nothing to say — the round it would have listed is
// the one already on screen above it. Only that case disappears: still loading, refused, or simply never
// routed all keep the panel, because each of those is something the reader needs told.
const collapsed = computed(() =>
  !!props.excludeRoundId && !loading.value && !failed.value && shownRounds.value.length === 0);

const roundColor = (status) =>
  ({ Approved: "positive", Rejected: "negative", Pending: "primary" }[status] || "grey-6");

// A ratio only meant something while a round could carry with an objection against it. One decline closes
// a round now, so "1/1" says nothing — and a round closed under the old two-decline threshold would read
// its own count against today's, as "2/1". The decisions underneath name every decliner either way.
const roundLabel = (r) => {
  if (r.status === "Approved") return "Approved";
  if (r.status === "Rejected") return r.declineCount > 1 ? `Declined by ${r.declineCount}` : "Declined";
  return "Open";
};

// Superseded is the one that needs saying in words: the round closed before this approver decided, which
// is not the same as them never responding.
const decisionLabel = (status) =>
  ({ Superseded: "Round closed", Pending: "Awaiting", Approved: "Approved", Rejected: "Declined" }[status] || status);

const decisionColor = (status) =>
  ({ Approved: "positive", Rejected: "negative", Superseded: "grey-6", Pending: "primary" }[status] || "grey-6");

// Whose signature a round is still waiting on. Only an OPEN round waits on anybody — a closed one holds
// nothing but history, and marking a row there as somebody's turn would ask for an action nobody can take.
const awaitingDecision = (round, d) => round?.status === "Pending" && d?.status === "Pending";

const load = async () => {
  failed.value = false;
  if (!props.engagementId) {
    rounds.value = [];
    return;
  }
  loading.value = true;
  try {
    rounds.value = (await remsApi.approvalHistory(props.engagementId)) || [];
  } catch {
    // A history that will not load is not worth an error banner over the page it decorates; the panel
    // says so in its own body instead.
    rounds.value = [];
    failed.value = true;
  } finally {
    loading.value = false;
  }
};

watch(() => props.engagementId, load, { immediate: true });
</script>

<style scoped>
.ah { border: 1px solid var(--line); border-radius: 10px; }
.ah__round { border-radius: 8px; }
.ah__head { background: var(--teal-050); }
/* The approvers an open round is still waiting on, marked the same way the round list on the approver's
   own task marks them, so the two lists read alike. */
.ah--awaiting {
  background: #fff8e1;
  box-shadow: inset 3px 0 0 #ff8f00;
}
</style>
