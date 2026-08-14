<template>
  <q-page padding>
    <app-detail-header :items="breadcrumbs" :back-to="{ name: 'rems_approvals' }">
      <template #actions>
        <q-badge v-if="task" :color="engagementStatus.color" class="q-pa-sm text-body2 q-mr-sm">
          {{ engagementStatus.label }}
        </q-badge>
        <q-badge v-if="task" :color="approvalStatusColor(task.status)" class="q-pa-sm text-body2">
          {{ approvalStatusLabel(task.status) }}
        </q-badge>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <q-banner v-else-if="errorMsg" class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else-if="task">
      <!-- Decision-state banner. -->
      <q-banner v-if="task.status === 'Approved'" dense class="bg-green-1 text-green-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_verified" color="green-9" /></template>
        You approved this task{{ task.decidedOnUtc ? ` on ${fmt.formatDateTime(task.decidedOnUtc)}` : "" }}.
      </q-banner>
      <q-banner v-else-if="task.status === 'Rejected'" class="bg-red-1 text-red-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_cancel" color="red-9" /></template>
        <div class="text-weight-medium">
          You rejected this task{{ task.decidedOnUtc ? ` on ${fmt.formatDateTime(task.decidedOnUtc)}` : "" }}.
        </div>
        <div v-if="task.rejectionReason" class="q-mt-xs" style="white-space: pre-wrap;">
          Reason: {{ task.rejectionReason }}
        </div>
      </q-banner>
      <q-banner v-else-if="!task.canDecide" dense class="bg-grey-2 text-grey-8 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_lock" color="grey-7" /></template>
        This approval round has closed. No further action is required on your task.
      </q-banner>
      <q-banner v-else dense class="bg-teal-1 text-blue-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_fact_check" color="blue-9" /></template>
        Review every tab below, complete your checklist, then approve — or reject with a reason.
      </q-banner>

      <div class="row q-col-gutter-md">
        <div class="col-12 col-md-8">
          <q-card flat bordered class="rems-card">
            <q-card-section class="row items-center q-py-sm">
              <div class="text-subtitle1 text-primary">
                <q-icon name="o_work" size="20px" class="q-mr-xs" />{{ request.remsNumber }} — {{ engagement.entity?.name || "Engagement" }}
              </div>
              <q-space />
              <div class="text-caption text-grey-7">Acting as {{ approverRoleLabel(task.role) }}</div>
            </q-card-section>
            <q-separator />

            <!-- The same material staff filled in, in the same order as the engagement workspace, plus the
                 request that started it. Read-only throughout: an approver reviews, never edits. -->
            <q-tabs
              v-model="tab" dense align="left" active-color="primary" indicator-color="primary"
              class="text-grey-7" no-caps inline-label
            >
              <q-tab v-for="t in TABS" :key="t.name" :name="t.name" :icon="t.icon" :label="t.label" />
            </q-tabs>
            <q-separator />

            <q-tab-panels v-model="tab" keep-alive animated>
              <!-- ---------- Request ---------- -->
              <q-tab-panel name="request">
                <div class="row q-col-gutter-md">
                  <div v-for="item in requestRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div v-if="item.type === 'status'">
                      <q-badge :color="requestStatusColor(request)">{{ requestStatusLabel(request) }}</q-badge>
                    </div>
                    <div v-else class="rems-value">
                      {{ item.value }}
                      <!-- One value, not a column of them, so the icon can advertise the tooltip here. -->
                      <template v-if="item.hint">
                        <q-icon name="o_info" size="14px" class="rems-value__info" />
                        <q-tooltip anchor="top middle" self="bottom middle" max-width="320px" :delay="300">
                          {{ item.hint }}
                        </q-tooltip>
                      </template>
                    </div>
                  </div>
                </div>

                <template v-if="request.description">
                  <q-separator class="q-my-md" />
                  <div class="rems-label">Description</div>
                  <!-- eslint-disable-next-line vue/no-v-html -->
                  <div class="rems-value rems-value--rich" v-html="renderRichText(request.description)" />
                </template>

                <q-separator class="q-my-md" />
                <div class="rems-label q-mb-xs">Attachments</div>
                <q-list v-if="request.files && request.files.length" dense separator>
                  <q-item v-for="f in request.files" :key="f.id" clickable tag="a" :href="fileUrl(f)" target="_blank">
                    <q-item-section avatar><q-icon name="o_description" color="grey-7" /></q-item-section>
                    <q-item-section>
                      <q-item-label class="rems-value">{{ f.fileName || "Attachment" }}</q-item-label>
                      <q-item-label caption>{{ formatSize(f.fileSize) }}</q-item-label>
                    </q-item-section>
                    <q-item-section side><q-icon name="o_open_in_new" color="grey-6" /></q-item-section>
                  </q-item>
                </q-list>
                <div v-else class="rems-value text-grey-6">No attachments.</div>
              </q-tab-panel>

              <!-- ---------- Client ---------- -->
              <q-tab-panel name="client">
                <div class="row q-col-gutter-md">
                  <div v-for="item in clientRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div class="rems-value">{{ item.value }}</div>
                  </div>
                </div>

                <q-separator class="q-my-md" />
                <div class="rems-label q-mb-xs">Entities</div>
                <q-list v-if="client.entities && client.entities.length" dense>
                  <q-item v-for="e in client.entities" :key="e.id" class="q-px-none">
                    <q-item-section avatar>
                      <q-icon :name="e.isMainEntity ? 'o_star' : 'o_apartment'" color="grey-7" />
                    </q-item-section>
                    <q-item-section>
                      <q-item-label class="rems-value">
                        {{ e.name }}
                        <q-badge v-if="e.id === engagement.entity?.id" color="primary" class="q-ml-xs">Under review</q-badge>
                      </q-item-label>
                      <q-item-label caption>
                        {{ e.ein ? `EIN ${e.ein}` : "No EIN" }} · {{ e.isMainEntity ? "Main entity" : "Related entity" }}
                      </q-item-label>
                    </q-item-section>
                  </q-item>
                </q-list>
                <div v-else class="rems-value text-grey-6">No entities.</div>
              </q-tab-panel>

              <!-- ---------- Setup ---------- -->
              <q-tab-panel name="setup">
                <!-- The entity's addresses + contacts, as they came in on the submitted form. -->
                <q-expansion-item icon="o_home_work" label="Addresses & contacts" dense-toggle class="rems-details q-mb-md">
                  <div class="q-pa-sm">
                    <div class="row q-col-gutter-md">
                      <div class="col-12 col-sm-6">
                        <div class="rems-label">Physical</div>
                        <div class="rems-value">{{ addressOf("Physical") }}</div>
                      </div>
                      <div class="col-12 col-sm-6">
                        <div class="rems-label">Mailing</div>
                        <div class="rems-value">{{ addressOf("Mailing") }}</div>
                      </div>
                    </div>
                    <div class="rems-label q-mt-sm">Contacts</div>
                    <div v-if="entityContacts.length" class="column q-gutter-xs">
                      <div v-for="c in entityContacts" :key="c.id" class="rems-value">
                        <span class="text-weight-medium">{{ roleText(c.role) }}:</span> {{ c.name || "—" }}
                        <span class="text-grey-6">({{ c.email || "no email" }} · {{ c.phone || "no phone" }})</span>
                      </div>
                    </div>
                    <div v-else class="rems-value text-grey-6">No contacts.</div>
                  </div>
                </q-expansion-item>

                <div class="row q-col-gutter-md">
                  <div v-for="item in setupRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div class="rems-value">{{ item.value }}</div>
                  </div>

                  <!-- Fee + realization are reserved to the Department Director / Managing Shareholder
                       (AC-REMS-019.10). Saying so beats an empty field that reads as "never filled in". -->
                  <div v-if="engagement.financialsRestricted" class="col-12">
                    <div class="rems-label">First-Year Fee Estimate · % Realization</div>
                    <div class="rems-value text-grey-6">
                      <q-icon name="o_lock" size="16px" class="q-mr-xs" />
                      Reserved for the Department Director and Managing Shareholder.
                    </div>
                  </div>
                </div>

                <!-- Audit: the signed client-acceptance form. -->
                <q-card v-if="engagement.audit" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_fact_check" size="18px" class="q-mr-xs" />Audit — Client Acceptance Form
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <q-banner v-if="engagement.audit.url" dense class="bg-green-1 text-green-9 rounded-borders">
                      <template #avatar><q-icon name="o_verified" color="green-9" /></template>
                      <div class="row items-center">
                        <div class="col">A signed client-acceptance form is on file.</div>
                        <q-btn
                          flat dense no-caps color="green-9" icon="o_open_in_new"
                          :label="engagement.audit.fileName || 'Open'"
                          :href="mediaUrl(engagement.audit.url)" target="_blank"
                        />
                      </div>
                    </q-banner>
                    <q-banner v-else dense class="bg-orange-1 text-orange-9 rounded-borders">
                      <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
                      No signed client-acceptance form is on file.
                    </q-banner>
                  </q-card-section>
                </q-card>

                <!-- Government audit: the contract block. -->
                <q-card v-if="engagement.government" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_gavel" size="18px" class="q-mr-xs" />Government Audit — Contract
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <div class="row q-col-gutter-md">
                      <div v-for="item in governmentRows" :key="item.label" class="col-12 col-sm-6">
                        <div class="rems-label">{{ item.label }}</div>
                        <div class="rems-value">{{ item.value }}</div>
                      </div>
                    </div>
                  </q-card-section>
                </q-card>

                <!-- Tax: fiscal year end, the calculated schedule, and the form checklist. -->
                <q-card v-if="engagement.tax" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_receipt_long" size="18px" class="q-mr-xs" />Tax — Fiscal Year &amp; Forms
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <div class="row q-col-gutter-md">
                      <div class="col-12 col-sm-6">
                        <div class="rems-label">Fiscal Year End</div>
                        <div class="rems-value">{{ dateOnly(engagement.tax.fiscalYearEnd) }}</div>
                      </div>
                      <div class="col-12 col-sm-6">
                        <div class="rems-label">Calculated Due Dates</div>
                        <div class="rems-value">
                          <template v-if="engagement.tax.dueDates">
                            Original: {{ dateOnly(engagement.tax.dueDates.originalDueDate) }} ·
                            Extended: {{ dateOnly(engagement.tax.dueDates.extendedDueDate) }}
                          </template>
                          <template v-else>—</template>
                        </div>
                      </div>
                    </div>
                    <div class="rems-label q-mt-md q-mb-xs">Tax Forms</div>
                    <div v-if="engagement.tax.taxForms && engagement.tax.taxForms.length" class="row q-gutter-xs">
                      <q-chip
                        v-for="f in engagement.tax.taxForms" :key="f.id" dense square outline color="primary"
                        :label="f.label"
                      />
                    </div>
                    <div v-else class="rems-value text-grey-6">No tax forms selected.</div>
                  </q-card-section>
                </q-card>
              </q-tab-panel>

              <!-- ---------- Marketing ---------- -->
              <q-tab-panel name="marketing">
                <div v-if="marketingGroups.length">
                  <div v-for="g in marketingGroups" :key="g.label" class="q-mb-md">
                    <div class="rems-label q-mb-xs">{{ g.label }}</div>
                    <div class="row q-gutter-xs">
                      <q-chip
                        v-for="m in g.items" :key="m.id" dense square outline color="primary" icon="o_campaign"
                        :label="m.label"
                      />
                    </div>
                  </div>
                </div>
                <div v-else class="rems-value text-grey-6">No marketing methods were tagged on this engagement.</div>
              </q-tab-panel>

              <!-- ---------- Commission ---------- -->
              <q-tab-panel name="commission">
                <q-list v-if="commissionSplits.length" bordered separator class="rounded-borders">
                  <q-item v-for="s in commissionSplits" :key="s.id">
                    <q-item-section avatar><q-icon name="o_payments" color="primary" /></q-item-section>
                    <q-item-section><q-item-label class="rems-value">{{ s.employee?.name || "—" }}</q-item-label></q-item-section>
                    <q-item-section side><q-badge color="primary">{{ s.percentage }}%</q-badge></q-item-section>
                  </q-item>
                  <q-item>
                    <q-item-section><q-item-label class="text-weight-medium">Total allocated</q-item-label></q-item-section>
                    <q-item-section side>
                      <q-badge :color="commissionTotal > 100 ? 'negative' : 'grey-7'">{{ commissionTotal }}%</q-badge>
                    </q-item-section>
                  </q-item>
                </q-list>
                <div v-else class="rems-value text-grey-6">No commission splits on this engagement.</div>
              </q-tab-panel>

              <!-- ---------- Approval ---------- -->
              <q-tab-panel name="approval">
                <div class="row q-col-gutter-md q-mb-md">
                  <div v-for="item in roundRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div class="rems-value">{{ item.value }}</div>
                  </div>
                </div>

                <div v-if="round.rejectionReason" class="q-mb-md">
                  <q-banner dense class="bg-red-1 text-red-9 rounded-borders">
                    <template #avatar><q-icon name="o_cancel" color="red-9" /></template>
                    <div class="text-weight-medium">This round was rejected.</div>
                    <div class="q-mt-xs" style="white-space: pre-wrap;">Reason: {{ round.rejectionReason }}</div>
                  </q-banner>
                </div>

                <!-- Server order: decided first, oldest decision leading, undecided at the bottom. -->
                <div class="rems-label q-mb-xs">Approvers on this round</div>
                <q-list bordered separator class="rounded-borders">
                  <q-item v-for="d in round.decisions" :key="d.taskId">
                    <q-item-section avatar><q-icon :name="approverRoleIcon(d.role)" color="primary" /></q-item-section>
                    <q-item-section>
                      <q-item-label class="text-weight-medium">
                        {{ d.approver?.name || "—" }}
                        <q-badge v-if="d.isYou" color="primary" class="q-ml-xs">You</q-badge>
                      </q-item-label>
                      <q-item-label caption>
                        {{ approverRoleLabel(d.role) }}
                        <template v-if="d.decidedOnUtc"> · {{ fmt.formatDateTime(d.decidedOnUtc) }}</template>
                      </q-item-label>
                      <q-item-label v-if="d.rejectionReason" caption class="text-red-9" style="white-space: pre-wrap;">
                        {{ d.rejectionReason }}
                      </q-item-label>
                    </q-item-section>
                    <q-item-section side>
                      <q-badge :color="approvalStatusColor(d.status)">{{ approvalStatusLabel(d.status) }}</q-badge>
                    </q-item-section>
                  </q-item>
                </q-list>
              </q-tab-panel>
            </q-tab-panels>
          </q-card>
        </div>

        <!-- Checklist + decision. -->
        <div class="col-12 col-md-4">
          <q-card flat bordered class="rems-card q-mb-md">
            <q-card-section class="text-subtitle1 text-weight-medium">
              Your Checklist
              <q-badge :color="allChecklistComplete ? 'positive' : 'grey-6'" class="q-ml-sm">
                {{ completedCount }}/{{ checklist.length }}
              </q-badge>
            </q-card-section>
            <q-separator />
            <q-list separator>
              <q-item v-for="item in checklist" :key="item.id">
                <q-item-section avatar>
                  <q-checkbox
                    :model-value="item.isCompleted"
                    :disable="!task.canDecide || busy || savingItemId === item.id"
                    color="primary"
                    @update:model-value="(val) => toggleItem(item, val)"
                  />
                </q-item-section>
                <q-item-section>
                  <q-item-label :class="item.isCompleted ? 'text-grey-6' : 'rems-value'">{{ item.label }}</q-item-label>
                </q-item-section>
                <q-item-section v-if="savingItemId === item.id" side><q-spinner color="primary" size="18px" /></q-item-section>
              </q-item>
              <q-item v-if="!checklist.length"><q-item-section class="text-grey-6">No checklist items.</q-item-section></q-item>
            </q-list>

            <template v-if="task.canDecide">
              <q-separator />
              <q-card-section>
                <div v-if="!allChecklistComplete" class="text-caption text-grey-7 q-mb-sm">
                  Complete every checklist item to enable Approve.
                </div>
                <div class="row q-col-gutter-sm">
                  <div class="col">
                    <q-btn
                      outline no-caps color="negative" icon="o_cancel" label="Reject" class="full-width"
                      :disable="busy" @click="rejectOpen = true"
                    />
                  </div>
                  <div class="col">
                    <q-btn
                      unelevated no-caps color="primary" icon="o_check_circle" label="Approve" class="full-width"
                      :loading="approving" :disable="approveDisabled" @click="approve"
                    >
                      <q-tooltip v-if="!allChecklistComplete">All checklist items must be completed first</q-tooltip>
                    </q-btn>
                  </div>
                </div>
              </q-card-section>
            </template>
          </q-card>

          <!-- The REQUEST's conversation, not a thread of its own: an approver's question needs to reach
               the partner who raised it and the CSE, who read it on the request detail and in the pool's
               Conversations dialog. A private per-task thread would be a dead end. -->
          <q-card v-if="request.remsId" flat bordered class="rems-card">
            <q-card-section class="text-subtitle1 text-weight-medium">Conversation</q-card-section>
            <q-separator />
            <q-card-section>
              <entity-notes-panel :entity-type="EntityType.Rems" :entity-id="request.remsId" />
            </q-card-section>
          </q-card>
        </div>
      </div>
    </template>

    <!-- Reject: a required reason (AC-REMS-020.1). -->
    <q-dialog v-model="rejectOpen" persistent>
      <q-card style="min-width: 380px; max-width: 90vw;">
        <q-card-section class="text-subtitle1 text-weight-medium">Reject this approval task</q-card-section>
        <q-separator />
        <q-card-section>
          <div class="text-body2 text-grey-8 q-mb-sm">
            The engagement is returned for rework. A reason is required and shared with the staff/CSE.
          </div>
          <q-form ref="rejectFormRef" greedy>
            <q-input
              v-model="rejectReason" outlined type="textarea" autogrow label="Reason for rejection *"
              hide-bottom-space :rules="[(v) => (!!v && v.trim().length > 0) || 'A reason is required']"
            />
          </q-form>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" :disable="rejecting" @click="cancelReject" />
          <q-btn unelevated no-caps color="negative" label="Reject" :loading="rejecting" @click="submitReject" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
// The REMS approval-task review screen (WO-117 Part B, AC-REMS-019/020). Read-only throughout: it renders
// the same case staff assembled in the engagement workspace — the originating request, the client, the
// entity's engagement setup with its conditional audit/government/tax detail, the marketing tags, the
// commission splits — plus the round's other decisions, because an approver cannot sensibly sign off on
// less than what was filled in. Option-set references arrive from the server already resolved to labels
// (the approver roles do not carry optionSets.read). The ONE thing still scoped by role is the fee
// estimate and realization (AC-REMS-019.10), which arrive null with `financialsRestricted` set so the
// Setup tab can say they are reserved rather than show an empty field.
//
// The per-role checklist gates Approve (disabled until every item is completed; the server re-verifies and
// a 409 is surfaced), and Reject requires a reason. Once decided, the task is read-only.
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { remsApi, mediaApi, EntityType, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { useRemsMeta } from "modules/rems/useRemsMeta";
import { addressText } from "modules/rems/remsAddress";
import { renderRichText } from "utils/richText";

import AppDetailHeader from "components/common/AppDetailHeader.vue";
// Explicit import: boot/components.js registers only the Zw* inputs globally, so without this the tag
// resolves to nothing and the Conversation card renders empty — no error, just a blank panel.
import EntityNotesPanel from "components/universal/EntityNotesPanel.vue";

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const {
  typeLabel, typeHint, requestStatusLabel, requestStatusColor,
  industryGroupLabel, emsStateLabel, submissionStateLabel, departmentLabel, serviceLineLabel,
  subServiceLineLabel, subIndustryLabel,
  approverRoleLabel, approverRoleIcon, approvalStatusLabel, approvalStatusColor, engagementStatusMeta
} = useRemsMeta();

const taskId = route.params.taskId;

const task = ref(null);
const loading = ref(true);
const errorMsg = ref("");
const busy = ref(false);
const approving = ref(false);
const savingItemId = ref(null);

// Same order as the staff workspace, with the request that started it in front.
const TABS = [
  { name: "request", icon: "o_assignment", label: "Request" },
  { name: "client", icon: "o_business", label: "Client" },
  { name: "setup", icon: "o_engineering", label: "Setup" },
  { name: "marketing", icon: "o_campaign", label: "Marketing" },
  { name: "commission", icon: "o_payments", label: "Commission" },
  { name: "approval", icon: "o_approval", label: "Approval" }
];
const tab = ref("setup");

const request = computed(() => task.value?.request || {});
const engagement = computed(() => task.value?.engagement || {});
const client = computed(() => engagement.value.client || {});
const round = computed(() => task.value?.round || {});
const engagementStatus = computed(() => engagementStatusMeta(engagement.value.status));
const entityContacts = computed(() => engagement.value.entity?.contacts || []);
const commissionSplits = computed(() => engagement.value.commissionSplits || []);
const commissionTotal = computed(() =>
  commissionSplits.value.reduce((sum, s) => sum + (Number(s.percentage) || 0), 0));

const checklist = computed(() => task.value?.checklist || []);
const completedCount = computed(() => checklist.value.filter((i) => i.isCompleted).length);
// Approve is enabled only when every checklist item is completed (AC-REMS-019.7).
const allChecklistComplete = computed(() => checklist.value.length > 0 && checklist.value.every((i) => i.isCompleted));
const approveDisabled = computed(() => !task.value?.canDecide || !allChecklistComplete.value || busy.value || !!savingItemId.value);

const breadcrumbs = computed(() => [
  { label: "Home", icon: "o_home", to: "/" },
  { label: "Approvals", to: { name: "rems_approvals" } },
  { label: request.value.remsNumber || "Approval Task" }
]);

const formatMoney = (v) => new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(Number(v) || 0);
const money = (v) => (v == null ? "—" : formatMoney(v));
const text = (v) => (v == null || v === "" ? "—" : v);
const yesNo = (v) => (v == null ? "—" : v ? "Yes" : "No");

// Calendar-date fields (DateOnly "YYYY-MM-DD") are shown as-is (MM-DD-YYYY), never timezone-shifted — the
// tenant-tz formatter would corrupt a date-only value (mirrors the workspace's handling).
const dateOnly = (v) => {
  if (!v) return "—";
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(v));
  return m ? `${m[2]}-${m[3]}-${m[1]}` : String(v);
};

const requestRows = computed(() => {
  const r = request.value;
  return [
    { label: "Request ID", value: r.remsNumber },
    // No Title row: a request has no title of its own any more — the client it is for is what names it.
    { label: "Requested Client", value: text(r.requestedClientName) },
    { label: "Type", value: typeLabel(r.type), hint: typeHint(r.type) },
    { label: "Request Status", type: "status" },
    { label: "Customer Email", value: text(r.customerEmail) },
    { label: "Customer Phone Number", value: text(r.customerMobileNumber) },
    { label: "Industry Group", value: r.industryGroup ? industryGroupLabel(r.industryGroup) : "—" },
    { label: "EMS Form State", value: emsStateLabel(r.emsFormState) },
    { label: "Client Submission", value: submissionStateLabel(r.clientSubmissionState) },
    { label: "Assigned Admin", value: text(r.assignedAdmin?.name) },
    { label: "CSE", value: text(r.cse?.name) },
    { label: "Requested By", value: `${r.requestedBy || "—"} · ${fmt.formatDateTime(r.createdOnUtc)}` }
  ];
});

const clientRows = computed(() => {
  const c = client.value;
  return [
    { label: "Name", value: text(c.name) },
    { label: "Email", value: text(c.email) },
    { label: "Phone Number", value: text(c.mobileNumber) },
    { label: "Referral Source", value: text(c.referralSource) },
    { label: "Billing Contact", value: text(c.billingContactName) },
    { label: "Billing Email", value: text(c.billingEmail) },
    { label: "Billing Address", value: addressText(c.billingAddress) }
  ];
});

const setupRows = computed(() => {
  const e = engagement.value;
  const rows = [
    { label: "Entity", value: `${e.entity?.name || "—"}${e.entity?.ein ? ` · EIN ${e.entity.ein}` : ""}` },
    { label: "Department", value: departmentLabel(e.department) },
    // Same industry-then-service sequence the setup form is filled in (the Industry Group itself is a
    // request field, so it sits in the request block above rather than here).
    { label: "Sub-Industry", value: subIndustryLabel(e.subIndustry) },
    { label: "Service Line", value: serviceLineLabel(e.serviceLine) },
    { label: "Sub-Service Line", value: subServiceLineLabel(e.subServiceLine) },
    { label: "Department Director", value: text(e.departmentDirector?.name) },
    { label: "Engagement Executive", value: text(e.engagementExecutive?.name) },
    { label: "Billing Manager", value: text(e.billingManager?.name) }
  ];
  // Withheld figures get their own explanatory block in the template instead of a blank pair of fields.
  if (!e.financialsRestricted) {
    rows.push(
      { label: "First-Year Fee Estimate", value: money(e.firstYearFeeEstimate) },
      { label: "% Realization", value: e.realizationPercentage == null ? "—" : `${e.realizationPercentage}%` }
    );
  }
  return rows;
});

const governmentRows = computed(() => {
  const g = engagement.value.government || {};
  return [
    { label: "Contract Number", value: text(g.contractNumber) },
    { label: "Florida 1% State Fee", value: yesNo(g.floridaOnePercentStateFeeApplies) },
    { label: "Contract Start", value: dateOnly(g.contractStartDate) },
    { label: "Contract End", value: dateOnly(g.contractEndDate) },
    { label: "Original Term", value: text(g.originalTerm) },
    { label: "Renewal Terms", value: text(g.renewalTerms) },
    { label: "PO Start", value: dateOnly(g.purchaseOrderStartDate) },
    { label: "PO End", value: dateOnly(g.purchaseOrderEndDate) }
  ];
});

const roundRows = computed(() => {
  const r = round.value;
  return [
    { label: "Round", value: `#${r.roundNumber}` },
    { label: "Round Status", value: approvalStatusLabel(r.status) },
    { label: "Sent", value: `${r.sentBy?.name || "—"} · ${fmt.formatDateTime(r.sentOnUtc)}` },
    { label: "Completed", value: r.completedOnUtc ? fmt.formatDateTime(r.completedOnUtc) : "—" }
  ];
});

// Marketing arrives already resolved and sorted; group it for display exactly as the workspace picker does.
// Items whose set carries no `group` metadata fall into one unnamed bucket rather than being dropped.
const marketingGroups = computed(() => {
  const groups = [];
  (engagement.value.marketingMethods || []).forEach((m) => {
    const label = m.group || "Marketing";
    const bucket = groups.find((g) => g.label === label);
    if (bucket) bucket.items.push(m);
    else groups.push({ label, items: [m] });
  });
  return groups;
});

const addressOf = (type) => {
  const row = (engagement.value.entity?.addresses || []).find((a) => a.addressType === type);
  return addressText(row?.address);
};
const roleText = (r) => (r || "").replace(/([a-z])([A-Z])/g, "$1 $2");
const fileUrl = (file) => mediaApi.absoluteUrl(file.url);
const mediaUrl = (url) => mediaApi.absoluteUrl(url);
const formatSize = (bytes) => {
  if (!bytes && bytes !== 0) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const load = async () => {
  loading.value = true;
  errorMsg.value = "";
  try {
    task.value = await remsApi.approvalTask(taskId);
  } catch (err) {
    errorMsg.value = getApiErrorMessage(err);
  } finally {
    loading.value = false;
  }
};

// Check / uncheck a checklist item; the server returns the updated item, which we adopt. On failure we
// re-sync from the server so the UI never diverges from the authoritative state.
const toggleItem = async (item, val) => {
  savingItemId.value = item.id;
  try {
    const updated = await remsApi.setChecklistItem(taskId, item.id, val);
    item.isCompleted = updated?.isCompleted ?? val;
    item.completedOnUtc = updated?.completedOnUtc ?? null;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    await load();
  } finally {
    savingItemId.value = null;
  }
};

const approve = async () => {
  const ok = await confirm({
    title: "Approve task",
    message: "Approve this engagement for your role? This decision is final.",
    confirmLabel: "Approve"
  });
  if (!ok) return;
  busy.value = true;
  approving.value = true;
  try {
    task.value = await remsApi.approveTask(taskId);
    notify.success("Task approved.");
  } catch (err) {
    // 409 = the server re-verified and the task is not approvable (checklist incomplete / already decided /
    // round closed). Surface the message and refresh so the UI reflects the true state (AC-REMS-019.8).
    if (err?.response?.status === 409) {
      notify.warning(getApiErrorMessage(err));
      await load();
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    busy.value = false;
    approving.value = false;
  }
};

const rejectOpen = ref(false);
const rejecting = ref(false);
const rejectReason = ref("");
const rejectFormRef = ref(null);

const cancelReject = () => {
  rejectOpen.value = false;
  rejectReason.value = "";
};

const submitReject = async () => {
  if (!(await rejectFormRef.value?.validate())) return;
  busy.value = true;
  rejecting.value = true;
  try {
    task.value = await remsApi.rejectTask(taskId, { reason: rejectReason.value.trim() });
    rejectOpen.value = false;
    rejectReason.value = "";
    notify.success("Task rejected; the engagement was returned for rework.");
  } catch (err) {
    if (err?.response?.status === 409) {
      notify.warning(getApiErrorMessage(err));
      await load();
      rejectOpen.value = false;
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    busy.value = false;
    rejecting.value = false;
  }
};

onMounted(load);
</script>

<style scoped>
.rems-card { border-radius: 12px; }
.rems-inner { border-radius: 10px; }
.rems-details { border: 1px solid #e0e6ed; border-radius: 10px; }
.rems-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: var(--q-primary);
  margin-bottom: 2px;
}
.rems-value { font-size: 14px; color: #2c3540; word-break: break-word; }
/* Marks a value that carries an explanation on hover; muted so it hints rather than competes. */
.rems-value__info { margin-left: 4px; color: var(--ink-300); cursor: help; vertical-align: text-bottom; }
/* Rich-text description: keep the request editor's paragraphs and lists readable inside the panel. */
.rems-value--rich :deep(p) { margin: 0 0 0.5em; }
.rems-value--rich :deep(p:last-child) { margin-bottom: 0; }
.rems-value--rich :deep(ul),
.rems-value--rich :deep(ol) {
  margin: 0 0 0.5em;
  padding-left: 1.25rem;
}
</style>
