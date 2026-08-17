<template>
  <q-page padding>
    <app-detail-header :items="breadcrumbs" :back-to="backTo">
      <template #actions>
        <!-- EVERY action on the request lives here. The header row itself is no-wrap and this is a badge,
             a status chip and up to half a dozen buttons — enough to run off the side of a laptop, never
             mind a phone — so it is wrapped in a flex box of its own that stacks onto a second line
             instead of pushing the Back button off the card.
             Stepping through the form is the one thing NOT here: Prev / Next / the finish actions sit on
             the tab strip, because they act on the tabs rather than on the request. -->
        <div class="rf-head">
          <q-badge v-if="request" :color="statusColor(request.status)">
            {{ statusLabel(request.status) }}
          </q-badge>

          <!-- What the page is doing with what has been typed. It stands in for the Save button it
               replaced, so it is present even while idle: a form with no Save on it has to say why. -->
          <div v-if="autoSaveOn" class="rf-save" :class="`rf-save--${saveChip.tone}`">
            <q-spinner v-if="saveState === 'saving'" size="14px" class="q-mr-xs" />
            <q-icon v-else :name="saveChip.icon" size="16px" class="q-mr-xs" />
            {{ saveChip.text }}
            <q-tooltip v-if="saveMessage">{{ saveMessage }}</q-tooltip>
          </div>

          <!-- Auto-save is a debounce, not a promise that everything already landed. Beside the chip that
               reports it, and only while something is genuinely outstanding — a way to force the point
               rather than a Save button that has to be pressed. -->
          <q-btn
            v-if="autoSaveOn && (savePending || saveState === 'error')"
            outline no-caps color="primary" icon="o_save" label="Save now"
            :loading="saveState === 'saving'" @click="flushSaves"
          />

          <!-- The mode switch. Offered only where the stage actually grants an edit right, so it is never
               a button that turns the page into a form nothing on it can be typed into. A new request has
               no mode to switch: it is a form and nothing else until it exists. -->
          <q-btn
            v-if="request && !isEditing && canEditAnything"
            unelevated no-caps color="primary" icon="o_edit" label="Edit"
            @click="setMode('edit')"
          />
          <q-btn
            v-if="request" outline no-caps color="primary" icon="o_forum" label="Conversation"
            @click="conversationOpen = true"
          />
          <q-btn
            v-if="hasSubmission" outline no-caps color="primary" icon="o_description"
            label="View submitted form" @click="submittedOpen = true"
          />
          <!-- What the client has actually been sent, and whether it landed. Beside Send Reminder rather
               than buried on a list, because "have we already chased them twice?" is the question asked
               immediately before pressing it. -->
          <q-btn
            v-if="canReadEmailLog" outline no-caps color="primary" icon="o_mark_email_read"
            label="Email log" @click="emailLogOpen = true"
          />

          <!-- The workflow moves: each one hands the request to somebody else. -->
          <!-- Submitting to the client belongs at the end of the tabs, where the work it completes is.
               This is the fallback for a request with no Commission tab to put it on — one raised before
               engagements existed, or one whose engagement is not this user's to see. -->
          <q-btn
            v-if="canSendToClient && !hasFinishTab" unelevated no-caps color="teal-7" icon="o_send"
            label="Submit to Client" :disable="!readyToSend" @click="openSend"
          >
            <q-tooltip v-if="!readyToSend">{{ sendBlockedReason }}</q-tooltip>
          </q-btn>
          <q-btn
            v-if="canRemind" unelevated no-caps color="amber-8" icon="o_notifications_active"
            label="Send reminder" @click="openReminder"
          />
          <q-btn
            v-if="canReturnToAdmin" unelevated no-caps color="primary" icon="o_assignment_turned_in"
            label="Return to admin" :loading="acting" @click="returnToAdmin"
          />
          <q-btn
            v-if="canSendBack" outline no-caps color="orange-9" icon="o_assignment_return"
            label="Send back to initiator" @click="sendBackOpen = true"
          />
        </div>
      </template>
    </app-detail-header>

    <acting-as-banner />

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="36px" /></div>

    <q-banner v-else-if="errorMsg" class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else>
      <!-- Why this request is back with its initiator, and who said so. Shown above everything because it
           is the instruction for the whole page. -->
      <q-banner v-if="openSendBack" dense class="rf-alert rf-alert--warn q-mb-md">
        <template #avatar><q-icon name="o_assignment_return" color="orange-9" /></template>
        <div class="text-weight-medium">Sent back by {{ openSendBack.returnedBy || "an admin" }}</div>
        <div>{{ openSendBack.reason }}</div>
      </q-banner>

      <q-banner v-if="declinedReasons.length" dense class="rf-alert rf-alert--reject q-mb-md">
        <template #avatar><q-icon name="o_gavel" color="red-9" /></template>
        <div class="text-weight-medium">The approvers declined this round</div>
        <ul class="q-my-xs q-pl-md">
          <li v-for="(r, i) in declinedReasons" :key="i">{{ r }}</li>
        </ul>
      </q-banner>

      <q-banner v-if="lockedReason" dense class="rf-alert rf-alert--lock q-mb-md">
        <template #avatar><q-icon name="o_lock" color="blue-9" /></template>
        {{ lockedReason }}
      </q-banner>

      <!-- ─── The form, one tab per part of the referral ──────────────────────────────────────────── -->
      <q-card flat bordered class="rf-card">
        <div class="rf-tabbar">
          <q-tabs
            v-model="tab" dense align="left" active-color="primary" indicator-color="primary"
            class="text-grey-7 rf-tabs col" no-caps inline-label
          >
            <q-tab
              v-for="t in tabs" :key="t.name" :name="t.name" :icon="t.icon" :label="t.label" :disable="t.disable"
            />
          </q-tabs>

          <div class="rf-tabs__end">
            <!-- The one thing the tab strip cannot say for itself — why the rest of it is greyed out —
                 parked at the end of the strip it is about, and only while it is still true. A banner
                 over the form said it louder than it deserves and pushed the form down for everyone. -->
            <q-icon v-if="tabsNote" name="o_info" size="20px" color="primary" class="rf-tabs__note">
              <q-tooltip anchor="bottom right" self="top right" max-width="320px" :delay="200">
                {{ tabsNote }}
              </q-tooltip>
            </q-icon>

            <!-- Step back. Absent on the first tab, and on a new request, where there is nowhere behind
                 the one tab that works. -->
            <q-btn
              v-if="prevTab" flat dense no-caps color="primary" icon="o_chevron_left" label="Prev"
              class="q-px-sm" @click="goTab(prevTab.name)"
            >
              <q-tooltip>{{ prevTab.label }}</q-tooltip>
            </q-btn>

            <!-- The ONE save left on the page — a request has to exist before anything can be auto-saved
                 against it, so on a brand-new request the create IS the step forward. It is gone the
                 moment it has run, which is also when the strip wants its width back. -->
            <q-btn
              v-if="isNew" unelevated no-caps dense color="primary" icon-right="o_arrow_right"
              label="Save as Draft &amp; Next" class="q-px-md" :loading="saving" @click="createDraft"
            />

            <!-- Filled while stepping on is the thing to do here, outlined where it shares the corner
                 with the two ways out — on that tab they are the point and this is the aside. -->
            <q-btn
              v-else-if="showNext" dense no-caps color="primary" icon-right="o_chevron_right"
              label="Next" class="q-px-md" :outline="atFinishTab" :unelevated="!atFinishTab"
              @click="goTab(nextTab.name)"
            >
              <q-tooltip>{{ nextTab.label }}</q-tooltip>
            </q-btn>

            <!-- Commission is where the initiator's own work ends: past it the tabs are the client's
                 answers and the approvers' round, neither of which they fill in. So instead of stepping
                 on, this is where the two ways out live. -->
            <template v-if="atFinishTab">
              <q-btn
                v-if="autoSaveOn" outline dense no-caps color="primary" icon="o_save"
                label="Save and Close" class="q-px-md" :loading="saveState === 'saving'"
                @click="saveAndClose"
              />
              <q-btn
                v-if="canSendToClient" unelevated dense no-caps color="teal-7" icon="o_send"
                label="Submit to Client" class="q-px-md" :disable="!readyToSend" @click="openSend"
              >
                <q-tooltip v-if="!readyToSend">{{ sendBlockedReason }}</q-tooltip>
              </q-btn>
            </template>
          </div>
        </div>
        <q-separator />

        <q-tab-panels v-model="tab" keep-alive animated>
          <!-- ---------- Client Information ---------- -->
          <q-tab-panel name="client">
            <detail-grid v-if="!isEditing" :rows="clientRows" />
            <!-- Entity Type and Industry are the page's to save — one belongs to the request's EMS form
                 record and the other to its engagement, neither to the client row — but this tab's to
                 lay out, because both describe the client. They are v-modelled down rather than rendered
                 in a row of their own up here. -->
            <client-information-fields
              v-else
              ref="clientFieldsRef"
              v-model="clientForm"
              v-model:industry-group="setupForm.industryGroup"
              v-model:sub-industry="setupForm.subIndustry"
              :readonly="!canEditClient"
              :client-locked="clientLocked"
              :setup-readonly="!canEditSetup"
              :industry-group-options="industryGroupOptions"
              :industry-locked="industryLocked"
              :sub-industry-options="subIndustryOptions"
              :admin-options="adminOptions"
              :type-options="typeOptions"
              :files="request?.files || []"
              :attempted="attempted"
              @change="markDirty('client')"
            />

            <!-- The client's other businesses, and whether each has been turned into its own request yet.
                 Shown whether the tab is being read or edited: it is a list with one action, not a field
                 anybody fills in. -->
            <additional-entities-panel
              v-if="additionalEntities.length" :rows="additionalEntities" class="q-mt-md"
              @create-ems="createFollowUp"
            />
          </q-tab-panel>

          <!-- ---------- Engagement Setup ---------- -->
          <q-tab-panel name="setup">
            <div v-if="!setupEngagement" class="text-grey-7">{{ noEngagementNote }}</div>

            <detail-grid v-else-if="!isEditing" :rows="setupRows" />

            <!-- The CSE is the page's to save (it belongs to the request's EMS form record, not to the
                 engagement) but the setup form's to lay out — it sits with the other two people who run
                 the engagement, so it is v-modelled down rather than rendered up here in a row of its
                 own. The entity type goes down read-only: the Government Audit card keys off it. -->
            <engagement-setup-form
              v-else
              ref="setupRef"
              v-model:cse-user-id="setupForm.cseUserId"
              :engagement="setupEngagement"
              :cse-options="cseOptions"
              :cse-hint="cseHint"
              :industry-group="setupForm.industryGroup"
              :dept-options="departmentOptions"
              :sub-service-line-options="subServiceLineOptions"
              :tax-form-options="taxFormOptions"
              :tax-form-unavailable="taxFormUnavailable"
              :department-directors="workspace?.departmentDirectors || []"
              :executive-options="executiveOptions"
              :billing-manager-options="billingManagerOptions"
              :billing-period-options="billingPeriodOptions"
              :editable="canEditSetup"
              @change="markDirty('setup')"
            />
          </q-tab-panel>

          <!-- ---------- Marketing ---------- -->
          <q-tab-panel v-if="setupEngagement" name="marketing">
            <detail-grid v-if="!isEditing" :rows="marketingRows" />
            <engagement-marketing
              v-else
              ref="marketingRef"
              :engagement="setupEngagement" :marketing-groups="marketingGroups"
              :marketing-unavailable="marketingUnavailable" :editable="canEditSetup"
              @change="markDirty('marketing')"
            />
          </q-tab-panel>

          <!-- ---------- Commission ---------- -->
          <q-tab-panel v-if="setupEngagement" name="commission">
            <detail-grid v-if="!isEditing" :rows="commissionRows" />
            <engagement-commission
              v-else
              ref="commissionRef"
              :engagement="setupEngagement" :recipient-options="cseOptions" :editable="canEditSetup"
              @change="markDirty('commission')"
            />
          </q-tab-panel>

          <!-- The Client Intake tab stood here: the materialised client record and the main entity's
               addresses, restated on a tab of their own. "View Submitted Form" in the header shows the
               client's answers already, from the immutable snapshot rather than from a second copy of
               them, so the tab was the same page twice. Its Other Entities list was not a restatement —
               it carries the Create EMS action — and moved up to the Client tab with the rest of what is
               known about the client. -->

          <!-- ---------- Approval ---------- -->
          <!-- Only ever reached by someone who may read it: the approver list is gated on managing
               engagements, so for everyone else this tab does not exist until a round has actually been
               opened, and then shows the record of it rather than the controls for running one. -->
          <q-tab-panel v-if="showApprovalTab" name="approval">
            <template v-if="engagement">
              <engagement-approval
                v-if="canManageApproval"
                :engagement="engagement" :can-send="canRouteForApproval"
                :marketing-saved="marketingComplete" @status-changed="load"
              />
              <q-banner v-else dense class="rf-alert rf-alert--lock">
                <template #avatar><q-icon name="o_gavel" color="blue-9" /></template>
                {{ approvalNote }}
              </q-banner>
              <approval-history :engagement-id="engagement.id" class="q-mt-md" />
            </template>
          </q-tab-panel>
        </q-tab-panels>
      </q-card>

      <!-- No action bar. Every button that stood here is in one of the two corners at the top of the page
           — the workflow moves in the header, stepping through the form on the tab strip — so the page
           ends on the last field of whichever tab is open, with nothing below it to scroll to. -->
    </template>

    <!-- All five act on a saved request, so none of them exist while one is being composed. -->
    <template v-if="remsId">
      <send-back-dialog v-model="sendBackOpen" :rems-number="request?.remsNumber" @confirm="sendBack" />
      <send-ems-dialog v-model="sendOpen" :rems-id="remsId" :subtitle="subtitle" @sent="load" />
      <send-ems-dialog
        v-model="reminderOpen" mode="reminder" :rems-id="remsId" :subtitle="subtitle" @sent="load"
      />
      <submitted-form-dialog v-model="submittedOpen" :rems-id="remsId" />
      <conversation-dialog v-model="conversationOpen" :request-id="remsId" :subtitle="subtitle" />
      <email-log-dialog v-model="emailLogOpen" :rems-id="remsId" :subtitle="subtitle" @sent="load" />
    </template>
  </q-page>
</template>

<script setup>
// THE REMS form (Phase 16): one tabbed page replacing the create/edit drawer, the entity tab strip and
// the four-step Setup/Marketing/Commission/Approval wizard.
//
// CREATING, EDITING AND READING ARE THE SAME PAGE, on three paths — /rems/requests/new,
// /rems/requests/edit/:id and /rems/requests/:id — because they are the same material seen three ways.
// Which one you are on is the URL itself: the page reads its own route NAME, and nothing is carried in a
// query flag. (It used to be one path with `:id = "new"` and `?mode=edit`, which made the plainest link in
// the module — "show me this request" — the longest.)
//
// The tabs are the parts of a referral — the client, the engagement setup, how it was won, who is paid for
// it, and the approval round — not the steps of a wizard: every tab but the first is reachable in any
// order and none of them gates another. What the CLIENT answered is not among them: it is a snapshot they
// sent rather than a part of the referral the firm writes, and the header opens it.
//
// THERE IS ONE SAVE ON THE PAGE, and it is only there because a request has to EXIST before anything can
// be written against it: saving the Client Information tab files the draft, and from that moment every
// edit on every tab saves itself (see the auto-save block below). The Save button that used to write the
// whole page at once is gone with it — it was the thing that made a half-filled request feel unsafe to
// leave, and it was also the reason the setup could be typed and then lost.
//
// The page is the same for everyone; what differs is what it lets you touch. Editability is derived from
// the request's STAGE rather than from which list you arrived by, because the two rework states hand the
// setup to the initiator while the client's own answers stay read-only to them — a split the old
// per-record locks could not express.
import { ref, reactive, computed, watch, nextTick, onMounted, onBeforeUnmount } from "vue";
import { useRoute, useRouter, onBeforeRouteLeave } from "vue-router";
import { remsApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { usePermissions, Permissions } from "composables/usePermissions";
import {
  useRemsMeta, useRemsOptionSets, useRemsEngagementOptionSets, useRemsIndustryGroups
} from "modules/rems/useRemsMeta";
import { useAutoSave } from "modules/rems/useAutoSave";
import { REMS_STATUS } from "modules/rems/remsStatus";

import AppDetailHeader from "components/common/AppDetailHeader.vue";
import ActingAsBanner from "modules/rems/components/ActingAsBanner.vue";
import DetailGrid from "modules/rems/components/DetailGrid.vue";
import ClientInformationFields from "modules/rems/components/ClientInformationFields.vue";
import AdditionalEntitiesPanel from "modules/rems/components/AdditionalEntitiesPanel.vue";
import ApprovalHistory from "modules/rems/components/ApprovalHistory.vue";
import SendBackDialog from "modules/rems/components/SendBackDialog.vue";
import EngagementSetupForm from "modules/rems/components/engagement/EngagementSetupForm.vue";
import EngagementMarketing from "modules/rems/components/engagement/EngagementMarketing.vue";
import EngagementCommission from "modules/rems/components/engagement/EngagementCommission.vue";
import EngagementApproval from "modules/rems/components/engagement/EngagementApproval.vue";
import SendEmsDialog from "modules/rems/components/SendEmsDialog.vue";
import SubmittedFormDialog from "modules/rems/components/SubmittedFormDialog.vue";
import ConversationDialog from "modules/rems/components/ConversationDialog.vue";
import EmailLogDialog from "modules/rems/components/EmailLogDialog.vue";

const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { has } = usePermissions();
const { statusLabel, statusColor, emsFormActivity } = useRemsMeta();
const { typeOptions, load: loadTypes } = useRemsOptionSets();
const { industryGroupOptions, load: loadIndustryGroups } = useRemsIndustryGroups();
const {
  departmentOptions, subServiceLineOptions, subIndustryOptions,
  marketingGroups, marketingUnavailable,
  taxFormOptions, taxFormUnavailable, billingPeriodOptions, load: loadEngagementOptions
} = useRemsEngagementOptionSets();

// The three routes this page answers on. Which one is live is the whole of "what am I doing here": there
// is no id sentinel and no mode flag to keep in step with it.
const ROUTE_NEW = "rems_request_new";
const ROUTE_EDIT = "rems_request_edit";
const ROUTE_VIEW = "rems_request";

const isNew = computed(() => route.name === ROUTE_NEW);
const remsId = computed(() => (isNew.value ? null : route.params.id || null));

const loading = ref(true);
const saving = ref(false);
const acting = ref(false);
const errorMsg = ref("");
const attempted = ref(false);

const request = ref(null);
const workspace = ref(null);
// The engagement as the SERVER last described it, re-read after each auto-save pass. Kept apart from the
// workspace because the two are wanted for opposite reasons: the tabs below are seeded from the workspace
// and must not be re-seeded while they are being typed into, while the approval tab and the
// send-for-approval gate have to follow what was actually written.
const engagementLive = ref(null);
// The workspace is refused to anyone the request is not about. Tracked so the setup tab can say which
// of the two silences it is — "not yours to work" or "this request never had an engagement".
const workspaceDenied = ref(false);
const sendBacks = ref([]);
const additionalEntities = ref([]);
const adminOptions = ref([]);
const cseOptions = ref([]);
const executiveOptions = ref([]);
const billingManagerOptions = ref([]);

const clientFieldsRef = ref(null);
const setupRef = ref(null);
const marketingRef = ref(null);
const commissionRef = ref(null);

const conversationOpen = ref(false);
const submittedOpen = ref(false);
const emailLogOpen = ref(false);
const sendOpen = ref(false);
const reminderOpen = ref(false);
const sendBackOpen = ref(false);

const blankClient = () => ({
  clientName: "",
  customerEmail: "",
  customerMobileNumber: "",
  description: "",
  type: "",
  existingClientReferenceId: null,
  assignAdminUserId: null
});
const clientForm = reactive(blankClient());
// The fields the page owns rather than a tab: each is written by an endpoint of its own, and two of the
// three are asked on the Client tab while the third is on Setup, so no single tab component can hold them.
// `industryGroup` is Entity Type and `subIndustry` is Industry — see the note at the top of useRemsMeta.
const setupForm = reactive({ cseUserId: null, industryGroup: null, subIndustry: null });

// ---- View vs Edit ----
// Two ways of looking at the same record: View renders every field as a label, Edit renders the controls.
// It is the route rather than component state, so an Edit link from a list lands straight in the form and
// a reload or a shared link keeps whichever the user was in. A request that does not exist yet is always
// the form — there is nothing to read.
//
// Independent of the stage rules below: this decides whether controls are SHOWN, those decide whether any
// of them accept input. A stage granting no edit right simply never offers the switch.
const isEditing = computed(() => isNew.value || route.name === ROUTE_EDIT);

const engagement = computed(() => engagementLive.value || workspace.value?.engagement || null);
const engagementId = computed(() => engagement.value?.id || null);

// The engagement a brand-new request will get, so the setup tab has a shape to render before there is
// anything to write it to. A stable object, not a fresh one per read: the setup form re-seeds itself
// whenever this changes identity, which on a new object every render would wipe what is being typed.
const newEngagement = Object.freeze({
  id: null,
  department: null,
  subServiceLine: null,
  departmentDirector: null,
  engagementExecutive: null,
  billingManager: null,
  firstYearFeeEstimate: null,
  realizationPercentage: null,
  billingPeriod: null,
  numberOfBills: null,
  status: "Draft",
  marketingMethodIds: [],
  commissionSplits: [],
  audit: null,
  government: null,
  tax: null
});
// What the three engagement tabs are SEEDED from — deliberately the loaded workspace rather than the
// live view above, so a background re-read after an auto-save cannot overwrite a field mid-keystroke.
const setupEngagement = computed(() => (isNew.value ? newEngagement : workspace.value?.engagement || null));

// A request being created is a draft in every respect but existing, so the stage rules below read it as one.
const status = computed(() => (isNew.value ? REMS_STATUS.DRAFT : request.value?.status || ""));
const subtitle = computed(() =>
  [request.value?.remsNumber, request.value?.clientName].filter(Boolean).join(" — "));

const breadcrumbs = computed(() => [
  { label: "Home", icon: "o_home", to: "/" },
  { label: cameFromReview.value ? "EMS Review" : "My Requests", to: backTo.value },
  { label: isNew.value ? "New Request" : request.value?.remsNumber || "Request" }
]);
// Which list to go back to: whichever one the user can actually work this request from.
const cameFromReview = computed(() =>
  !isNew.value && has(Permissions.RemsEngagementsManage) && !isInitiatorStage.value);
const backTo = computed(() => (cameFromReview.value ? "/rems/ems-review" : "/rems/partner"));

// ---- Who may touch what, by stage ----
const isInitiatorStage = computed(() =>
  [REMS_STATUS.DRAFT, REMS_STATUS.AWAITING_CUSTOMER, REMS_STATUS.RETURNED_TO_INITIATOR,
    REMS_STATUS.CHANGES_REQUESTED].includes(status.value));
const isAdminStage = computed(() =>
  [REMS_STATUS.ADMIN_REVIEW, REMS_STATUS.AWAITING_ADMIN_CONFIRMATION].includes(status.value));
// Everything freezes once a round is open, and stays frozen once approved.
const frozen = computed(() =>
  [REMS_STATUS.PENDING_APPROVAL, REMS_STATUS.APPROVED].includes(status.value));

const isAdmin = computed(() => has(Permissions.RemsEngagementsManage));

// The client tab belongs to the initiator while the request is theirs, and to the admin while it is his.
// In the two REWORK states it is read-only even to the initiator: only the setup was sent back.
const canEditClient = computed(() => {
  if (frozen.value) return false;
  if (isAdminStage.value) return isAdmin.value;
  return [REMS_STATUS.DRAFT, REMS_STATUS.AWAITING_CUSTOMER].includes(status.value);
});
const canEditSetup = computed(() => {
  if (frozen.value) return false;
  if (isAdminStage.value) return isAdmin.value;
  return isInitiatorStage.value;
});
// Whether there is anything on this page this user could change at this stage — what decides if the Edit
// switch is offered at all. Every tab that edits is one of these two now: what the client submitted is
// read from the snapshot, not corrected here.
const canEditAnything = computed(() => canEditClient.value || canEditSetup.value);

// Whether the page has anything of its own to write. Same answer as above, kept separate because they are
// separate questions — one asks what this user may change, the other what the page's Save writes.
const canSaveForm = computed(() => canEditClient.value || canEditSetup.value);

// The intake form has gone out naming this client, at this address and number. All three are settled from
// that moment: the invite cannot be un-sent, and a request that then names somebody else — or somewhere
// else to reach them — is a record of a conversation that never happened. Draft is the window to change
// who the request is for; sending it is what closes it.
const clientLocked = computed(() => !isNew.value && status.value !== REMS_STATUS.DRAFT);
const industryLocked = computed(() => !isNew.value && status.value !== REMS_STATUS.DRAFT);

const lockedReason = computed(() => {
  if (status.value === REMS_STATUS.PENDING_APPROVAL) {
    return "This request is with the approvers. Every field is read-only until they decide.";
  }
  if (status.value === REMS_STATUS.APPROVED) return "This engagement is approved and permanently read-only.";
  if (status.value === REMS_STATUS.AWAITING_CUSTOMER && !canEditClient.value) {
    return "The intake form is with the client.";
  }
  return "";
});

const noEngagementNote = computed(() => (workspaceDenied.value
  ? "The engagement setup is with whoever the request is with — you can read the request itself on the first tab."
  : "This request has no engagement on file. It was raised before the setup moved onto this page."));

// ---- Actions available now ----
// None of them apply to a request that does not exist yet: they all act on a saved record.
const canSendToClient = computed(() =>
  !isNew.value && status.value === REMS_STATUS.DRAFT && has(Permissions.RemsFormsSend));
const canRemind = computed(() =>
  status.value === REMS_STATUS.AWAITING_CUSTOMER && has(Permissions.RemsFormsSend));
// There is a log to read once something has been emailed; before the intake link goes out it would open
// on nothing. The reminder inside it is gated separately, by the server.
const canReadEmailLog = computed(() =>
  !isNew.value && has(Permissions.RemsEmailLogRead) && emsFormActivity(request.value));
const canReturnToAdmin = computed(() =>
  [REMS_STATUS.RETURNED_TO_INITIATOR, REMS_STATUS.CHANGES_REQUESTED].includes(status.value));
const canSendBack = computed(() => isAdminStage.value && isAdmin.value);
const canRouteForApproval = computed(() =>
  isAdminStage.value && has(Permissions.RemsApprovalsSend));

// The lighter of the two completeness bars: enough to ask the client for their details. The full one —
// the engagement team, realization, a marketing method, the signed CAF on an audit — is enforced when the
// round is actually routed, by the API.
const readyToSend = computed(() =>
  !!clientForm.customerEmail?.trim() && !!setupForm.cseUserId && !!setupForm.industryGroup);
const sendBlockedReason = computed(() => {
  if (!clientForm.customerEmail?.trim()) return "The client has no email address to send the form to.";
  if (!setupForm.cseUserId) return "Choose a CSE first — it is on the Engagement Setup tab.";
  if (!setupForm.industryGroup) {
    return "Choose an entity type on the Client Information tab — it decides what the client is asked.";
  }
  return "";
});

// Whether the client has answered — what puts "View Submitted Form" in the header.
const hasSubmission = computed(() => !!workspace.value?.client);
const marketingComplete = computed(() => (engagement.value?.marketingMethodIds?.length || 0) > 0);
const openSendBack = computed(() => sendBacks.value.find((s) => !s.resolvedOnUtc) || null);
const declinedReasons = ref([]);

const cseHint = computed(() => (cseOptions.value.length
  ? ""
  : "No members in the \"CSE\" group — add them in Administration → User Groups."));

// ---- The Approval tab ----
// Reading the approver list is gated on managing engagements. The initiator does not hold that, so the
// section used to load, call it, and render a bare 403 on a tab they could do nothing with anyway.
//
// Approval is not their step: the admin opens the round once the client's intake is in. So for anyone who
// cannot run one, the tab appears only once a round HAS been opened — at which point there is something
// worth reading, and the approval history (gated on reading requests, which they do hold) is what they get.
const ROUND_OPENED = ["PendingApproval", "Approved", "Rejected"];
const approvalStarted = computed(() => ROUND_OPENED.includes(engagement.value?.status));
const canManageApproval = computed(() => isAdmin.value);
const showApprovalTab = computed(() =>
  !!engagement.value && (canManageApproval.value || approvalStarted.value));

const approvalNote = computed(() => {
  const s = engagement.value?.status;
  if (s === "PendingApproval") return "This engagement is with its approvers. You will be notified when they decide.";
  if (s === "Approved") return "This engagement is fully approved.";
  if (s === "Rejected") {
    return "The approvers declined this round and the setup is back with you. Revise it, then return the request to the admin.";
  }
  return "The admin opens the approval round once the client's intake has been reviewed.";
});

// ---- Tabs ----
// One per part of the referral. Which ones exist depends on the record rather than on the user's progress:
// no Marketing or Commission where there is no engagement to hang them off, and no Approval until there is
// a round to show. What the client answered is not a tab — "View Submitted Form" in the header opens the
// snapshot they sent.
const TABS = [
  { name: "client", label: "Client Information", icon: "o_person" },
  { name: "setup", label: "Engagement Setup", icon: "o_work" },
  { name: "marketing", label: "Marketing", icon: "o_campaign" },
  { name: "commission", label: "Commission", icon: "o_payments" },
  { name: "approval", label: "Approval", icon: "o_approval" }
];

const tabs = computed(() => {
  const shown = {
    client: true,
    setup: true,
    marketing: !!setupEngagement.value,
    commission: !!setupEngagement.value,
    approval: showApprovalTab.value
  };
  return TABS.filter((t) => shown[t.name]).map((t) => ({
    ...t,
    // Everything past the first tab is written against a request id, and a request being composed has
    // none until its first save.
    disable: isNew.value && t.name !== "client"
  }));
});

// What the greyed-out tabs mean, for the one state in which they are greyed out. Empty the rest of the
// time, and the icon that carries it goes with it — there is nothing to explain about a strip where
// every tab works.
const tabsNote = computed(() => (isNew.value
  ? "Start with the client. Saving this first tab files the request as a draft, which is what the " +
    "remaining tabs are filled against — and from that point everything you type saves itself."
  : ""));

// In the URL like `mode`, so a reload or a shared link comes back to the tab that was open. A tab that is
// not on this record (a stale link, or one whose section has gone) falls back to the first rather than
// rendering an empty card.
const tab = computed({
  get: () => {
    const wanted = route.query.tab;
    return tabs.value.some((t) => t.name === wanted && !t.disable) ? wanted : "client";
  },
  set: (name) => {
    router.replace({ query: { ...route.query, tab: name === "client" ? undefined : name } });
  }
});

// ---- Stepping through the tabs ----
// The strip is the map; these are the path through it, so nobody has to know which tab comes next. Both
// skip over anything disabled, which on a new request is every tab but the first — so a request being
// composed has no step of its own and the create button below stands in for one.
const goTab = (name) => { tab.value = name; };

const stepTab = (delta) => {
  const list = tabs.value;
  for (let i = list.findIndex((t) => t.name === tab.value) + delta; i >= 0 && i < list.length; i += delta) {
    if (!list[i].disable) return list[i];
  }
  return null;
};
const prevTab = computed(() => stepTab(-1));
const nextTab = computed(() => stepTab(1));

// Where the initiator's own work ends. What lies past Commission is somebody else's: the approvers' round.
const FINISH_TAB = "commission";
const hasFinishTab = computed(() => tabs.value.some((t) => t.name === FINISH_TAB && !t.disable));
const atFinishTab = computed(() =>
  tab.value === FINISH_TAB && (autoSaveOn.value || canSendToClient.value));
// Finishing does not REPLACE stepping on. For the initiator in draft there is nothing past Commission, so
// the two ways out are all there is — but once a round has been opened, Approval sits behind it and the
// admin reviewing it still needs the step.
const showNext = computed(() => !!nextTab.value);

// ---- View mode: the same fields, as a record rather than a form ----
// Resolved through the same option lists the pickers use, so a code the tenant has relabelled reads the
// same in both modes — and an unknown code shows itself rather than rendering blank.
const labelOf = (options, value) =>
  (value ? options.find((o) => o.value === value)?.label || value : "");

const nameOf = (options, id) => (id ? options.find((o) => o.value === id)?.label || "" : "");

const currency = (v) =>
  (v === null || v === undefined || v === "" ? "" : `$${Number(v).toLocaleString()}`);

// Marketing options arrive grouped for the picker; flattened here to turn stored ids back into labels.
const marketingLabels = computed(() => {
  const byId = new Map();
  (marketingGroups.value || []).forEach((g) => (g.items || []).forEach((i) => byId.set(i.value, i.label)));
  return (engagement.value?.marketingMethodIds || []).map((id) => byId.get(id) || id);
});

const commissionLabels = computed(() =>
  (engagement.value?.commissionSplits || [])
    .map((s) => `${s.employee?.name || nameOf(cseOptions.value, s.employeeId)} — ${s.percentage}%`));

const clientRows = computed(() => [
  { label: "Client", value: clientForm.clientName },
  { label: "Client Email Address", value: clientForm.customerEmail },
  { label: "Client Phone Number", value: clientForm.customerMobileNumber },
  { label: "Relationship to THF", value: labelOf(typeOptions.value, clientForm.type) },
  { label: "Entity Type", value: labelOf(industryGroupOptions.value, setupForm.industryGroup) },
  { label: "Industry", value: labelOf(subIndustryOptions.value, setupForm.subIndustry) },
  { label: "Reviewing Admin", value: nameOf(adminOptions.value, clientForm.assignAdminUserId) },
  { label: "Message from Partner", value: clientForm.description, wide: true, html: true },
  {
    label: "Attachments",
    value: (request.value?.files || []).map((f) => f.fileName).filter(Boolean),
    wide: true
  }
]);

const setupRows = computed(() => {
  const e = engagement.value;
  if (!e) return [];
  // The same sequence the form is filled in, so reading a request and typing one describe it in the
  // same order. Entity Type and Industry are not here — they are read on the Client tab, where they are
  // now asked.
  return [
    { label: "Service Line", value: labelOf(subServiceLineOptions.value, e.subServiceLine) },
    { label: "Department", value: labelOf(departmentOptions.value, e.department) },
    { label: "Department Director", value: e.departmentDirector?.name },
    { label: "CSE", value: nameOf(cseOptions.value, setupForm.cseUserId) },
    { label: "Engagement Executive", value: e.engagementExecutive?.name },
    { label: "Billing Manager", value: e.billingManager?.name },
    { label: "First-Year Fee Estimate", value: currency(e.firstYearFeeEstimate) },
    { label: "% Realization", value: e.realizationPercentage == null ? "" : `${e.realizationPercentage}%` },
    { label: "Billing Period", value: labelOf(billingPeriodOptions.value, e.billingPeriod) },
    { label: "No. of Bills", value: e.numberOfBills },
    // The conditional blocks say nothing when they do not apply to this engagement, so they are hidden
    // rather than shown empty — unlike the fields above, where blank IS the record.
    { label: "Fiscal Year End", value: e.tax?.fiscalYearEnd, hideWhenEmpty: true },
    { label: "Contract Number", value: e.government?.contractNumber, hideWhenEmpty: true },
    {
      label: "Signed Client Acceptance Form",
      value: e.audit ? (e.audit.clientAcceptanceFormMediaId ? "On file" : "Not yet provided") : "",
      hideWhenEmpty: true
    }
  ];
});

const marketingRows = computed(() => [{ label: "Marketing", value: marketingLabels.value, wide: true }]);
const commissionRows = computed(() => [{ label: "Commission", value: commissionLabels.value, wide: true }]);

// ---- Load ----
// Every people picker here is scoped to a user group of the same name, kept in Administration → User
// Groups. An absent or empty group yields an empty list on purpose rather than quietly offering everyone.
const GROUPS = {
  cse: "CSE",
  executive: "Engagement Executive",
  billingManager: "Billing Manager"
};

const toOptions = (rows) => (rows || []).map((r) => ({ label: r.name, value: r.id }));

const loadPickers = async () => {
  const [admins, cse, execs, billing] = await Promise.all([
    remsApi.admins().catch(() => []),
    remsApi.admins(GROUPS.cse).catch(() => []),
    remsApi.admins(GROUPS.executive).catch(() => []),
    remsApi.admins(GROUPS.billingManager).catch(() => [])
  ]);
  adminOptions.value = toOptions(admins);
  cseOptions.value = toOptions(cse);
  executiveOptions.value = toOptions(execs);
  billingManagerOptions.value = toOptions(billing);
};

// Entity Type and Industry as they were picked BEFORE the request existed. Both are asked on the first
// tab, which a request being composed can fill in, and neither can be written at create time: the entity
// type is filed with the CSE and the endpoint requires the pair, and the industry needs an engagement id.
// So they are carried across the reload that follows the create — `seedForms` reads a server that does
// not have them yet, and would otherwise wipe two answers the user just gave.
let pendingSetupPick = null;

const seedForms = (detail, ws) => {
  clientForm.clientName = detail.clientName || "";
  clientForm.customerEmail = detail.customerEmail || "";
  clientForm.customerMobileNumber = detail.customerMobileNumber || "";
  clientForm.description = detail.description || "";
  clientForm.type = detail.type || "";
  clientForm.existingClientReferenceId = detail.existingClientReferenceId || null;
  clientForm.assignAdminUserId = detail.assignedAdmin?.id || null;
  setupForm.cseUserId = detail.cse?.id || null;
  setupForm.industryGroup =
    ws?.industryGroup || detail.industryGroup || pendingSetupPick?.industryGroup || null;
  setupForm.subIndustry = ws?.engagement?.subIndustry || pendingSetupPick?.subIndustry || null;
};

// The reasons behind the LAST failed round, so the initiator sees what to fix without opening history.
const loadDeclineReasons = async (id) => {
  if (!id || status.value !== REMS_STATUS.CHANGES_REQUESTED) return [];
  const rounds = await remsApi.approvalHistory(id).catch(() => []);
  const last = [...rounds].reverse().find((r) => r.status === "Rejected");
  return (last?.decisions || [])
    .filter((d) => d.status === "Rejected" && d.reason)
    .map((d) => `${d.approver} (${d.role}): ${d.reason}`);
};

// The engagement as the server now holds it, WITHOUT re-seeding the tabs from it — read after the page's
// own writes so the approval tab and the send-for-approval gate follow what actually landed.
const refreshEngagement = async () => {
  if (isNew.value || !remsId.value) return;
  const ws = await remsApi.engagement(remsId.value).catch(() => null);
  if (ws) engagementLive.value = ws.engagement || null;
};

// ---- What the page writes ----
// Only the client half is enforced, because it is what the API requires to accept a request at all. The
// engagement setup saves as far as it has been filled — an initiator raising a referral may not know the
// fee or the billing manager yet. Completeness is enforced where it bites: sending the intake link needs a
// CSE and an industry group, and routing for approval needs the rest, which the API checks.
const clientProblem = () => {
  if (!clientForm.clientName?.trim()) return "Search for the client, or type the new client's name.";
  if (!clientForm.type) return "Choose how this referral relates to THF's records.";
  // The email, specifically. A mobile number does not stand in for it: the intake form is emailed, so a
  // request without an address cannot be sent to the client at all. (The API still accepts either, so
  // that requests raised elsewhere — a follow-up EMS off an entity with only a phone number — are not
  // refused outright; they simply cannot be saved from this form until the address is filled in.)
  if (!clientForm.customerEmail?.trim()) {
    return "Give the client's email address — the intake form is emailed to them.";
  }
  if (!clientForm.assignAdminUserId) return "Name the admin who will review this request.";
  return "";
};

const clientPayload = () => ({
  description: clientForm.description || null,
  type: clientForm.type,
  clientName: clientForm.clientName,
  customerEmail: clientForm.customerEmail || null,
  customerMobileNumber: clientForm.customerMobileNumber || null,
  existingClientReferenceId: clientForm.existingClientReferenceId || null
});

// ---- Auto-save ----
// Everything after the first save writes itself, part by part, in the order the records depend on each
// other: the request, then the CSE + entity type on its intake form, then the engagement — the client's
// industry first, because it is asked on the first tab and must save without the setup tab ever having
// been opened.
//
// A part that is not fillable yet returns why instead of failing — a commission recipient added a second
// ago has no percentage on them, and that is someone still typing, not an error to shout about.
const autoSaveOn = computed(() =>
  !isNew.value && !!remsId.value && isEditing.value && canSaveForm.value);

// What the request said last time the page read or wrote it. Compared before a write so opening a record,
// touching nothing, and switching tabs does not file a save of the identical thing.
let clientBaseline = "";
const clientSnapshot = () =>
  JSON.stringify({ ...clientPayload(), assignAdminUserId: clientForm.assignAdminUserId });

const {
  state: saveState, message: saveMessage, pending: savePending,
  mark: markDirty, flush: flushSaves, suspend: suspendSaves, resume: resumeSaves, reset: resetSaves
} = useAutoSave({
  client: async () => {
    if (!canEditClient.value) return "";
    const problem = clientProblem();
    if (problem) {
      // The chip says the tab cannot be saved; this is what points at the field that is why.
      attempted.value = true;
      return problem;
    }

    // The fields and the attachments are marked by the same flag but are two different writes, and
    // either can be the only one there is — a file picked with nothing retyped must still upload.
    if (clientSnapshot() !== clientBaseline) {
      request.value = await remsApi.update(remsId.value, {
        ...clientPayload(),
        // Said only when it changed: a null on its own means "leave the assignment alone".
        ...(clientForm.assignAdminUserId !== (request.value?.assignedAdmin?.id || null)
          ? { assignAdminUserId: clientForm.assignAdminUserId }
          : {})
      });
      clientBaseline = clientSnapshot();
    }
    // Uploaded now rather than on selection, so the files land on a request that exists.
    const mediaIds = (await clientFieldsRef.value?.uploadAttachments(remsId.value)) || [];
    if (mediaIds.length) request.value = await remsApi.addFiles(remsId.value, mediaIds);
    return "";
  },
  form: async () => {
    if (!canEditSetup.value) return "";
    // CSE and Entity Type live on the EMS form record, which is what the client's link is minted from
    // (`industryGroup` on the wire — see the note at the top of useRemsMeta). Both or neither: the
    // endpoint requires the pair, and the two are asked on different tabs, so the reason says where the
    // missing one is rather than leaving the user to hunt for it.
    if (!setupForm.cseUserId || !setupForm.industryGroup) {
      return setupForm.industryGroup
        ? "The CSE and the Entity Type are saved together — choose a CSE on the Engagement Setup tab."
        : "The CSE and the Entity Type are saved together — choose an Entity Type on the Client Information tab.";
    }
    await remsApi.saveForm(remsId.value, {
      cseUserId: setupForm.cseUserId,
      industryGroup: setupForm.industryGroup
    });
    return "";
  },
  // The client's trade. Asked on the Client tab but stored on the ENGAGEMENT, so the page writes it rather
  // than the setup form below: that form exists only once its own tab has been opened, and a field on the
  // first tab cannot depend on somebody having visited the second.
  industry: async () => {
    if (!canEditSetup.value || !engagementId.value) return "";
    // Empty string, not null — the endpoint reads null as "leave this field alone", so clearing the
    // picker has to say so out loud or the old value comes back on the next read.
    await remsApi.updateEngagement(engagementId.value, { subIndustry: setupForm.subIndustry ?? "" });
    return "";
  },
  // Only a request raised before engagements existed has none, and there is nowhere to put the setup for
  // one of those. Every request created since has one — raised in the same transaction as the request.
  setup: async () => {
    if (!canEditSetup.value || !engagementId.value) return "";
    await setupRef.value?.saveSetup(engagementId.value, remsId.value);
    return "";
  },
  marketing: async () => {
    if (!canEditSetup.value || !engagementId.value) return "";
    return (await marketingRef.value?.saveMarketing(engagementId.value)) || "";
  },
  commission: async () => {
    if (!canEditSetup.value || !engagementId.value) return "";
    await commissionRef.value?.saveCommission(engagementId.value);
    return "";
  }
}, {
  enabled: () => autoSaveOn.value,
  onSaved: refreshEngagement
});

// The state the page owns itself. The tabs below own theirs and announce it (@change).
watch(clientForm, () => markDirty("client"), { deep: true });
// `setupForm` is two writes, so it is two flags rather than one deep watcher: the CSE and the entity type
// go to the EMS form record together, the industry to the engagement.
watch([() => setupForm.cseUserId, () => setupForm.industryGroup], () => markDirty("form"));
watch(() => setupForm.subIndustry, () => markDirty("industry"));

const saveChip = computed(() => ({
  saving: { tone: "busy", icon: "o_sync", text: "Saving…" },
  saved: { tone: "ok", icon: "o_cloud_done", text: "All changes saved" },
  pending: { tone: "busy", icon: "o_edit", text: "Unsaved changes" },
  blocked: { tone: "warn", icon: "o_pending", text: "Not saved yet" },
  error: { tone: "bad", icon: "o_cloud_off", text: "Not saved" }
}[saveState.value] || { tone: "idle", icon: "o_cloud_sync", text: "Saves as you type" }));

const load = async () => {
  // Seeding writes to the very state the watchers above listen to, and that is the page catching up with
  // the server rather than the user typing.
  suspendSaves();
  try {
    // Nothing to read for a request that does not exist: the blank form IS the state.
    if (isNew.value) {
      loading.value = false;
      return;
    }
    loading.value = true;
    errorMsg.value = "";
    try {
      // A refused workspace is not a failed page: the request itself still reads, and the setup tab
      // says why it is empty. Settled rather than awaited so one outcome cannot hide the other.
      const [detail, wsResult] = await Promise.all([
        remsApi.get(remsId.value),
        remsApi.engagement(remsId.value).then((ws) => ({ ws }), () => ({ denied: true }))
      ]);
      const ws = wsResult.ws || null;
      workspaceDenied.value = !!wsResult.denied;
      request.value = detail;
      workspace.value = ws;
      engagementLive.value = ws?.engagement || null;
      seedForms(detail, ws);
      sendBacks.value = await remsApi.sendBacks(remsId.value).catch(() => []);
      additionalEntities.value = ws?.additionalEntities || [];
      declinedReasons.value = await loadDeclineReasons(ws?.engagement?.id);
    } catch (err) {
      errorMsg.value = getApiErrorMessage(err);
    } finally {
      loading.value = false;
    }
  } finally {
    clientBaseline = clientSnapshot();
    // A tick, so the tabs re-rendering off the freshly seeded state settles before edits count again.
    await nextTick();
    resetSaves();
    resumeSaves();
    // Whatever was picked before the request existed is writable against it now. Marked rather than
    // written here so it goes through the same savers as any other edit — including the entity type's
    // "choose a CSE too", which is what the user lands on the Setup tab to do.
    if (pendingSetupPick) {
      if (pendingSetupPick.industryGroup) markDirty("form");
      if (pendingSetupPick.subIndustry) markDirty("industry");
      pendingSetupPick = null;
    }
  }
};

// One request per page, but the page outlives the id: creating one replaces "new" with its id, and the
// Create-EMS action on another of the client's businesses moves it to a different request entirely. Both
// are the same route record, so the component is re-used and has to re-read rather than assume its first
// load still describes what is on screen.
watch(remsId, (id) => {
  if (id) load();
});

// Leaving a tab commits what was typed on it rather than waiting out the debounce, and arriving at
// Approval re-reads the engagement so the round reflects the setup that was just saved.
watch(tab, async (name) => {
  await flushSaves();
  if (name === "approval") await refreshEngagement();
});

// Reading and editing are two paths now, so switching between them is a navigation rather than a query
// flip. Replace, not push: it is the same record either way and Back should leave the page, not toggle it.
// The query rides along so the tab you were on survives the switch.
const setMode = async (mode) => {
  await flushSaves();
  router.replace({
    name: mode === "edit" ? ROUTE_EDIT : ROUTE_VIEW,
    params: { id: remsId.value },
    query: { ...route.query }
  });
};

// ---- The one save: filing the draft ----
// A request has to exist before anything else on this page can be written against it, so the first tab is
// committed by hand. Everything from here is auto-saved.
const createDraft = async () => {
  attempted.value = true;
  const problem = clientProblem();
  if (problem) {
    notify.warning(problem);
    return;
  }

  saving.value = true;
  // The create writes the client row only. The two classifications on this tab belong to records that do
  // not exist until it returns, so they ride across the reload instead (see `pendingSetupPick`).
  pendingSetupPick = { industryGroup: setupForm.industryGroup, subIndustry: setupForm.subIndustry };
  // Held outside the try: once the request exists, this page is about THAT request whatever fails
  // afterwards, or a second attempt would file a second copy of it.
  let created = null;
  try {
    created = await remsApi.create({
      ...clientPayload(),
      assignAdminUserId: clientForm.assignAdminUserId
    });
    const mediaIds = (await clientFieldsRef.value?.uploadAttachments(created.id)) || [];
    if (mediaIds.length) await remsApi.addFiles(created.id, mediaIds);
    notify.success(`${created.remsNumber} saved as a draft. Everything from here saves itself.`);
  } catch (err) {
    const detail = getApiErrorMessage(err);
    // A failure after the request itself was written is not "nothing happened", and saying so would send
    // the user back to a form they think is unsaved.
    notify.error(created
      ? `${created.remsNumber} was created, but its attachments did not save: ${detail}`
      : detail);
  } finally {
    saving.value = false;
    // Leaves the URL on the request that now exists, open for editing, on the tab that comes next.
    if (created) {
      router.replace({ name: ROUTE_EDIT, params: { id: created.id }, query: { tab: "setup" } });
    } else {
      // Nothing was created, so nothing reloads and the picks are still on screen where they were made.
      pendingSetupPick = null;
    }
  }
};

// ---- Workflow moves ----
// Each of these hands the request to somebody else, so whatever is still sitting in the debounce goes
// with it.
const openSend = async () => {
  await flushSaves();
  sendOpen.value = true;
};

// Done for now: commit the debounce and go back to the list. It refuses to leave on anything the last
// pass could not write — "Close" over unsaved work is the one thing auto-save must never do quietly.
const saveAndClose = async () => {
  await flushSaves();
  if (savePending.value) {
    notify.warning(saveMessage.value || "Some changes have not saved yet — they are still on this page.");
    return;
  }
  router.push(backTo.value);
};

const openReminder = async () => {
  await flushSaves();
  reminderOpen.value = true;
};

const sendBack = async (reason) => {
  acting.value = true;
  try {
    await flushSaves();
    await remsApi.sendBack(remsId.value, reason);
    notify.success("Sent back to the initiator.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    acting.value = false;
    sendBackOpen.value = false;
  }
};

const returnToAdmin = async () => {
  acting.value = true;
  try {
    await flushSaves();
    await remsApi.returnToAdmin(remsId.value);
    notify.success("Returned to the admin for confirmation.");
    await load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    acting.value = false;
  }
};

// Raise a fresh request for one of the client's other businesses, pre-filled from the contact they gave.
const createFollowUp = async (row) => {
  try {
    await flushSaves();
    const follow = await remsApi.create({
      clientName: row.fullName,
      customerEmail: row.emailAddress || undefined,
      customerMobileNumber: row.phoneNumber || undefined,
      type: clientForm.type,
      assignAdminUserId: clientForm.assignAdminUserId,
      fromAdditionalEntityId: row.id
    });
    notify.success(`${follow.remsNumber} created for ${row.fullName}.`);
    router.push({ name: ROUTE_EDIT, params: { id: follow.id } });
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// Navigating away commits the debounce; closing the tab cannot be awaited, so that one is a warning.
onBeforeRouteLeave(async () => { await flushSaves(); });

const warnOnUnload = (e) => {
  if (!savePending.value) return;
  e.preventDefault();
  e.returnValue = "";
};

onMounted(async () => {
  window.addEventListener("beforeunload", warnOnUnload);
  await Promise.all([loadTypes(), loadIndustryGroups(), loadEngagementOptions(), loadPickers()]);
  await load();
});

onBeforeUnmount(() => window.removeEventListener("beforeunload", warnOnUnload));
</script>

<style scoped>
.rf-card {
  border-radius: 12px;
}
.rf-tabs {
  padding: 0 4px;
  /* col gives flex-basis 0; without this the strip cannot shrink past its tabs and its own overflow
     arrows never appear — it just pushes the note off the card instead. */
  min-width: 0;
}
.rf-tabbar {
  display: flex;
  flex-wrap: nowrap;
  align-items: center;
}

/* The right corner of the strip: the note about the tabs, and the step through them.
   flex-shrink 0 so the tabs give up their width first and fall back to their own overflow arrows. */
.rf-tabs__end {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
  padding: 6px 12px 6px 8px;
}

/* On a phone there is no width to share: three dense buttons and a six-tab strip on one line leaves
   neither readable. The actions take their own line under the tabs instead. */
@media (max-width: 599px) {
  .rf-tabbar { flex-wrap: wrap; }
  .rf-tabs { flex: 1 1 100%; }
  .rf-tabs__end {
    width: 100%;
    justify-content: flex-end;
    flex-wrap: wrap;
    border-top: 1px solid var(--line);
    padding: 8px 12px;
  }
}
/* A cursor that says "hover me": the icon carries the whole message and nothing about an icon otherwise
   suggests there is more behind it. */
.rf-tabs__note { cursor: help; }

.rf-hint {
  font-size: 12.5px;
  color: var(--ink-500);
  margin-bottom: 14px;
}

/* min-width:0 is what lets this shrink inside the header's no-wrap row; without it the box refuses to go
   below the width of everything in it and nothing ever wraps. */
.rf-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  min-width: 0;
}

/* The save indicator. It replaces a button, so it is sized and spaced like one rather than like a badge
   tucked into the corner — a form with no Save on it has to be obvious about where its saving went. */
.rf-save {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  border-radius: 8px;
  font-size: 12.5px;
  font-weight: 500;
  white-space: nowrap;
}
.rf-save--idle { border: 1px solid var(--line); color: var(--ink-500); }
.rf-save--busy { background: var(--teal-050); color: var(--teal-900); }
.rf-save--ok { background: #e8f5ec; color: #1b5e34; }
.rf-save--warn { background: #fff4e5; color: #8a5300; }
.rf-save--bad { background: #fdecea; color: #8a1c12; }

.rf-alert {
  border-radius: 10px;
}
.rf-alert--warn { background: #fff4e5; color: #8a5300; }
.rf-alert--reject { background: #fdecea; color: #8a1c12; }
.rf-alert--lock { background: var(--teal-050); color: var(--teal-900); }

/* The action bar that used to close the page is gone — every button is in the header or on the tab strip
   now, so the form ends on its last field. */
</style>
