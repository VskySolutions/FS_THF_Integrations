<template>
  <q-page padding>
    <app-detail-header :items="breadcrumbs" :back-to="{ name: 'rems_approvals' }">
      <template #actions>
        <!-- The ENGAGEMENT's status, which is the request's answer rather than the reader's: it turns
             Approved only when the round does. The tooltip says so, since "Pending Approval" beside a
             task the reader has already signed is exactly the pair that reads as a contradiction. -->
        <app-option-badge v-if="task" :option="engagementStatus" />
        <!-- The reader's OWN decision is deliberately not a second badge up here. It belongs to them, not
             to the request, and beside the engagement's status it read as a competing answer to the same
             question — "Approved" next to "Pending Approval" on one header. It is on the banner below and
             on their row in the Approvers list, both of which say whose decision it is. -->
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <q-banner v-else-if="errorMsg" class="bg-red-1 text-red-9 rounded-borders">
      <template #avatar><q-icon name="o_error" color="red-9" /></template>
      {{ errorMsg }}
    </q-banner>

    <template v-else-if="task">
      <!-- Decision-state banner. -->
      <!-- Signing is the reader's own act, and it is NOT the request being approved. A round of four is
           approved when all four have signed, so the banner says what is still outstanding rather than
           leaving a green box to imply the whole thing is done. -->
      <q-banner
        v-if="task.status === 'Approved'" dense
        :class="`${roundOutstanding ? 'bg-teal-1 text-teal-9' : 'bg-green-1 text-green-9'} rounded-borders q-mb-md`"
      >
        <template #avatar>
          <q-icon :name="roundOutstanding ? 'o_hourglass_top' : 'o_verified'" :color="roundOutstanding ? 'teal-9' : 'green-9'" />
        </template>
        <div>
          You approved this task{{ task.decidedOnUtc ? ` on ${fmt.formatDateTime(task.decidedOnUtc)}` : "" }}.
        </div>
        <div v-if="roundOutstanding" class="q-mt-xs">
          The request is not approved yet — {{ roundApprovedCount }} of {{ roundDecisions.length }} approvers
          have signed, and it becomes Approved only once all of them have.
        </div>
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
        This approval is already closed. No further action is required.
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
              <!-- ---------- Client Form ---------- -->
              <!-- What the CLIENT answered on their intake form: who they are, how to reach them, where
                   they are, who to speak to, and the other businesses they named. The addresses and
                   contacts are here rather than on the tab beside it because they are the client's own
                   answers too — they were asked on this form and nowhere else. -->
              <q-tab-panel name="clientForm">
                <div class="row q-col-gutter-md">
                  <div v-for="item in clientRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div class="rems-value">{{ item.value }}</div>
                  </div>
                </div>

                <q-separator class="q-my-md" />
                <div class="row q-col-gutter-md">
                  <div class="col-12 col-sm-6">
                    <div class="rems-label">Physical Address</div>
                    <div class="rems-value">{{ addressOf("Physical") }}</div>
                  </div>
                  <div class="col-12 col-sm-6">
                    <div class="rems-label">Mailing Address</div>
                    <div class="rems-value">{{ addressOf("Mailing") }}</div>
                  </div>
                </div>

                <q-separator class="q-my-md" />
                <div class="rems-subhead">Contacts</div>
                <div v-if="entityContacts.length" class="column q-gutter-xs">
                  <div v-for="c in entityContacts" :key="c.id" class="rems-value">
                    <span class="text-weight-medium">{{ roleText(c.role) }}:</span> {{ c.name || "—" }}
                    <span class="text-grey-6">({{ c.email || "no email" }} · {{ c.phone || "no phone" }})</span>
                  </div>
                </div>
                <div v-else class="rems-value text-grey-6">No contacts.</div>

                <q-separator class="q-my-md" />
                <div class="rems-subhead">Entities</div>
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

              <!-- ---------- Client Information ---------- -->
              <!-- The request the FIRM raised about this client — who it is for, how it is classified, who
                   is on it, and what was attached to it. The same tab the staff request page calls Client
                   Information, so the two read as one screen seen from two sides. -->
              <q-tab-panel name="request">
                <div class="row q-col-gutter-md">
                  <div v-for="item in requestRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div v-if="item.type === 'status'">
                      <app-option-badge :option="requestStatusOption(request)" />
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
                <div class="rems-subhead">Attachments</div>
                <!-- The same preview row the request form shows, minus the ✕: an approver reads the
                     packet, they do not edit it. Clicking opens the document in a new tab. -->
                <div v-if="request.files && request.files.length" class="column q-gutter-xs">
                  <app-stored-file-item v-for="f in request.files" :key="f.id" :file="f" />
                </div>
                <div v-else class="rems-value text-grey-6">No attachments.</div>
              </q-tab-panel>

              <!-- ---------- Setup ---------- -->
              <q-tab-panel name="setup">
                <div class="row q-col-gutter-md">
                  <div v-for="item in setupRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div class="rems-value">{{ item.value }}</div>
                  </div>

                  <!-- Fee + realization are reserved to the Department Director (AC-REMS-019.10).
                       Saying so beats an empty field that reads as "never filled in". -->
                  <div v-if="engagement.financialsRestricted" class="col-12">
                    <div class="rems-label">First-Year Fee Estimate · % Realization</div>
                    <div class="rems-value text-grey-6">
                      <q-icon name="o_lock" size="16px" class="q-mr-xs" />
                      Reserved for the Department Director.
                    </div>
                  </div>
                </div>

                <!-- Every card below appears on exactly the rule the setup FORM asked its questions on —
                     the engagement's department, and for the contract block the client's entity type
                     beside it — rather than on whether a detail row happens to exist. The two differ in
                     both directions: an audit engagement whose CAF was never uploaded has no audit row and
                     was showing the approver nothing at all about a document their sign-off depends on,
                     and an engagement moved off Tax keeps the tax row it was written with and went on
                     showing a due-date schedule that no longer applies to it. -->

                <!-- Audit and Assurance: the signed client-acceptance form, and for Assurance the client's
                     fiscal year end and the administrative fees underneath it. -->
                <q-card v-if="showAttest" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_fact_check" size="18px" class="q-mr-xs" />{{ attestCardTitle }}
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <template v-if="cafFile">
                      <q-banner dense class="bg-green-1 text-green-9 rounded-borders q-mb-sm">
                        <template #avatar><q-icon name="o_verified" color="green-9" /></template>
                        A signed client-acceptance form is on file.
                      </q-banner>
                      <app-stored-file-item :file="cafFile" />
                    </template>
                    <q-banner v-else dense class="bg-orange-1 text-orange-9 rounded-borders">
                      <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
                      No signed client-acceptance form is on file.
                    </q-banner>

                    <div v-if="showAssurance" class="row q-col-gutter-md q-mt-xs">
                      <div v-for="item in assuranceRows" :key="item.label" class="col-12 col-sm-6">
                        <div class="rems-label">{{ item.label }}</div>
                        <div class="rems-value">{{ item.value }}</div>
                      </div>
                    </div>
                  </q-card-section>
                </q-card>

                <!-- Government audit: the contract block. An Audit department on a Government entity, which
                     is the same pair the setup form keys it off. Not shown for a GCS engagement, whose own
                     card is below — the two share a stored row but answer different questions. -->
                <q-card v-if="showGovernment" flat bordered class="rems-inner q-mt-md">
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

                <!-- GCS: the purchase order the engagement is set up against, and the rate it is staffed
                     at. Shown for a GCS department only — the card above it is the government AUDIT's
                     contract block, which shares the same stored row but answers a different question. -->
                <q-card v-if="showGcs" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_request_quote" size="18px" class="q-mr-xs" />GCS — Purchase Order &amp; Rate
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <div class="row q-col-gutter-md">
                      <div v-for="item in gcsRows" :key="item.label" class="col-12 col-sm-6">
                        <div class="rems-label">{{ item.label }}</div>
                        <div class="rems-value">{{ item.value }}</div>
                      </div>
                    </div>
                    <!-- The purchase order itself, openable, the same way the signed CAF is. -->
                    <div v-if="purchaseOrderFile" class="q-mt-md">
                      <div class="rems-subhead">Purchase Order</div>
                      <app-stored-file-item :file="purchaseOrderFile" />
                    </div>
                  </q-card-section>
                </q-card>

                <!-- Tax: fiscal year end, the due-date schedule, and the form checklist. -->
                <q-card v-if="showTax" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_receipt_long" size="18px" class="q-mr-xs" />Tax — Fiscal Year &amp; Forms
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <!-- The dates the engagement was SENT with. They are derived from the fiscal year end
                         and then editable, so an approver reads what staff actually recorded rather than
                         what the rule would produce today. -->
                    <div class="row q-col-gutter-md">
                      <div class="col-12 col-sm-4">
                        <div class="rems-label">Fiscal Year End</div>
                        <div class="rems-value">{{ dateOnly(engagement.tax?.fiscalYearEnd) }}</div>
                      </div>
                      <div class="col-12 col-sm-4">
                        <div class="rems-label">Original Due Date</div>
                        <div class="rems-value">{{ dateOnly(engagement.tax?.dueDates?.originalDueDate) }}</div>
                      </div>
                      <div class="col-12 col-sm-4">
                        <div class="rems-label">First Extension Due Date</div>
                        <div class="rems-value">{{ dateOnly(engagement.tax?.dueDates?.extendedDueDate) }}</div>
                      </div>
                    </div>
                    <div class="rems-subhead q-mt-md">Tax Forms</div>
                    <div v-if="taxForms.length" class="row q-gutter-xs">
                      <q-chip
                        v-for="f in taxForms" :key="f.id" dense square outline color="primary"
                        :label="f.label"
                      />
                    </div>
                    <div v-else class="rems-value text-grey-6">No tax forms selected.</div>
                  </q-card-section>
                </q-card>

                <!-- CAS: how the client is billed. Client Accounting Services is the recurring arrangement
                     — how often the client is invoiced and how that billing actually runs are part of what
                     is being approved, and until now the approver's packet did not carry either of them. -->
                <q-card v-if="showBilling" flat bordered class="rems-inner q-mt-md">
                  <q-card-section class="q-py-sm text-subtitle2 text-primary">
                    <q-icon name="o_receipt" size="18px" class="q-mr-xs" />CAS — Billing
                  </q-card-section>
                  <q-separator />
                  <q-card-section>
                    <div class="row q-col-gutter-md">
                      <div class="col-12 col-sm-4">
                        <div class="rems-label">Billing Frequency</div>
                        <div class="rems-value">{{ text(billingPeriodLabel(engagement.billingPeriod)) }}</div>
                      </div>
                      <div class="col-12 col-sm-8">
                        <div class="rems-label">Description of Billing Process</div>
                        <!-- As staff typed it. The box it was written in grows with the text, so a
                             three-line schedule is three lines here rather than one run-on sentence. -->
                        <div class="rems-value" style="white-space: pre-wrap;">
                          {{ text(engagement.billingProcessDescription) }}
                        </div>
                      </div>
                    </div>
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
                <!-- Where the ROUND stands, as a badge rather than a word in a column of values: it is the
                     one thing on this tab everybody came to read, and "Partially Approved" is the answer
                     the old label could not give — a round of four with two signatures on it was Pending,
                     which reads as nobody having looked at it. -->
                <div class="row q-col-gutter-md q-mb-md">
                  <div class="col-12 col-sm-6">
                    <div class="rems-label">Approval Status</div>
                    <div>
                      <app-option-badge :option="roundMeta" />
                    </div>
                  </div>
                  <div v-for="item in roundRows" :key="item.label" class="col-12 col-sm-6">
                    <div class="rems-label">{{ item.label }}</div>
                    <div class="rems-value">{{ item.value }}</div>
                  </div>
                </div>

                <div v-if="round.rejectionReason" class="q-mb-md">
                  <q-banner dense class="bg-red-1 text-red-9 rounded-borders">
                    <template #avatar><q-icon name="o_cancel" color="red-9" /></template>
                    <div class="text-weight-medium">This request was rejected.</div>
                    <div class="q-mt-xs" style="white-space: pre-wrap;">Reason: {{ round.rejectionReason }}</div>
                  </q-banner>
                </div>

                <!-- Server order: by role — shareholder, director, CSE, commission recipient, then anyone
                     added by hand — so the list reads the same way every time, and a row does not move
                     under the reader each time somebody signs.
                     A round asks everybody at once rather than one after another, so "whose turn is it" is
                     answered by every row still waiting, not by one of them. Those are the rows marked
                     here; the reader's own is marked hardest, because it is the only one they can act
                     on. -->
                <div class="rems-subhead">Approvers</div>
                <q-list bordered separator class="rounded-borders">
                  <q-item
                    v-for="d in round.decisions" :key="d.taskId"
                    :class="{ 'ar--awaiting': awaitingDecision(d), 'ar--you': d.isYou }"
                  >
                    <q-item-section avatar>
                      <!-- The icon on the ROLE's own option — the tenant's, like its name. Amber while
                           the round is still waiting on this approver, so the list can be scanned down
                           its left edge for the rows that have not answered. -->
                      <q-icon
                        :name="approverRoleOption(d.role).icon || 'o_person'"
                        :color="awaitingDecision(d) ? 'amber-9' : 'primary'"
                      />
                    </q-item-section>
                    <q-item-section>
                      <q-item-label class="text-weight-medium">
                        {{ d.approver?.name || "—" }}
                        <q-badge v-if="d.isYou" color="primary" class="q-ml-xs">You</q-badge>
                        <q-badge
                          v-if="awaitingDecision(d)" color="amber-9" class="q-ml-xs"
                          :label="d.isYou ? 'Your turn' : 'Awaiting decision'"
                        />
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
                      <app-option-badge :option="approvalStatusOption(d.status)" />
                    </q-item-section>
                  </q-item>
                </q-list>

                <!-- What the approvers objected to BEFORE now. A resubmission is routed afresh rather than
                     reopening the last attempt, so those earlier objections are readable nowhere else on
                     this page. The current ones are left out of it: the list above already gives them in
                     full. Absent entirely where there is nothing earlier to read. -->
                <approval-history
                  v-if="engagement.engagementId" :engagement-id="engagement.engagementId"
                  :exclude-round-id="round.id" class="q-mt-md"
                />
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
            <!-- Taller than the panel's own default: this is a page column rather than a dialog, and a
                 thread worth reading beside the task deserves the room. -->
            <q-card-section>
              <entity-conversation-panel
                :entity-type="EntityType.Rems" :entity-id="request.remsId" height="520px"
              />
            </q-card-section>
          </q-card>
        </div>
      </div>
    </template>

    <!-- Reject: a required reason (AC-REMS-020.1). -->
    <q-dialog v-model="rejectOpen" persistent>
      <q-card style="width: 380px; max-width: 90vw;">
        <q-card-section class="text-subtitle1 text-weight-medium">Reject this approval task</q-card-section>
        <q-separator />
        <q-card-section>
          <div class="text-body2 text-grey-8 q-mb-sm">
            The engagement is returned for rework. A reason is required and shared with the staff/CSE.
          </div>
          <q-form ref="rejectFormRef" greedy>
            <app-text-field
              v-model="rejectReason" label="Reason for rejection" required type="textarea" autogrow
              :rules="[(v) => (!!v && v.trim().length > 0) || 'A reason is required']"
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
import { remsApi, EntityType, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat, formatDateOnly } from "composables/useDateFormat";
import {
  useRemsMeta, isAssuranceDepartment, isGcsDepartment, isCasDepartment, isTaxDepartment,
  isGovernmentAudit, requiresClientAcceptanceForm
} from "modules/rems/useRemsMeta";
import { addressText } from "modules/rems/remsAddress";
import { renderRichText } from "utils/richText";

import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppOptionBadge from "components/common/AppOptionBadge.vue";
import AppStoredFileItem from "components/common/AppStoredFileItem.vue";
import AppTextField from "components/common/AppTextField.vue";
// Explicit import: boot/components.js registers only the Zw* inputs globally, so without this the tag
// resolves to nothing and the Conversation card renders empty — no error, just a blank panel.
import EntityConversationPanel from "components/universal/EntityConversationPanel.vue";
import ApprovalHistory from "modules/rems/components/ApprovalHistory.vue";

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
// Everything a value is rendered with — its wording, its colour, its icon, the sentence on its tooltip —
// comes off the option itself. The *Option helpers hand back the whole thing for AppOptionBadge; the
// *Label ones are for the rows that read as plain text rather than as a badge.
const {
  typeLabel, typeHint, requestStatusOption,
  industryGroupLabel, formStatusOption, submissionStateOption,
  departmentLabel, subServiceLineLabel, subIndustryLabel, personnelLevelLabel, billingPeriodLabel,
  approverRoleLabel, approverRoleOption, approvalStatusOption,
  engagementStatusOption, roundStatusOption
} = useRemsMeta();

const taskId = route.params.taskId;

const task = ref(null);
const loading = ref(true);
const errorMsg = ref("");
const busy = ref(false);
const approving = ref(false);
const savingItemId = ref(null);

// The packet in the order it was assembled: what the client sent, what the firm recorded about them, the
// engagement built on top of it, how it was won, who is paid for it, and the round being decided. The last
// four are named exactly as the staff request page names them, so an approver and the admin who filled it
// in are looking at the same six words.
const TABS = [
  { name: "clientForm", icon: "o_description", label: "Client Form" },
  { name: "request", icon: "o_assignment", label: "Client Information" },
  { name: "setup", icon: "o_engineering", label: "Engagement Setup" },
  { name: "marketing", icon: "o_campaign", label: "Marketing" },
  { name: "commission", icon: "o_payments", label: "Commission" },
  { name: "approval", icon: "o_approval", label: "Approvals" }
];
// Opens on Approval, not on the packet: the approver came here to decide, and that tab carries the
// round, where the other approvers stand and their own decision. The tabs before it are the material
// they read on the way to it, left in the order it was filled in.
const tab = ref("approval");

const request = computed(() => task.value?.request || {});
const engagement = computed(() => task.value?.engagement || {});
const client = computed(() => engagement.value.client || {});
const round = computed(() => task.value?.round || {});
const engagementStatus = computed(() => engagementStatusOption(engagement.value.status));

// Whose signature the round is still waiting on. Only meaningful while the round is open: once it closes,
// a task left undecided is Superseded rather than Pending, and a closed round is waiting on nobody — so
// nothing on a finished round should read as somebody's turn.
const awaitingDecision = (d) => round.value.status === "Pending" && d?.status === "Pending";

// ---- Where the whole round stands ----
// Counted off the decisions the packet carries rather than taken from the round's own status alone: the
// round is Pending from the moment it is sent until the last approver signs, so the status by itself
// cannot tell "nobody has looked at this" from "everybody but you has signed". roundStatusOption turns the
// pair into the badge — Partially Approved, with the tally on its tooltip.
const roundDecisions = computed(() => round.value.decisions || []);
const roundApprovedCount = computed(() => roundDecisions.value.filter((d) => d.status === "Approved").length);
const roundMeta = computed(() =>
  roundStatusOption(round.value.status, roundApprovedCount.value, roundDecisions.value.length));
// The reader has signed but the request has not been approved — what keeps a green "all done" banner off
// a round that is still waiting on somebody else.
const roundOutstanding = computed(() => round.value.status === "Pending");
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

// Calendar dates read MM/DD/YYYY and are never timezone-shifted — see formatDateOnly.
const dateOnly = formatDateOnly;

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
    { label: "Entity Type", value: r.industryGroup ? industryGroupLabel(r.industryGroup) : "—" },
    // Read off the option, so the wording and the explanation are the tenant's own — the same two rows
    // the request lists render as badges.
    {
      label: "EMS Form State",
      value: formStatusOption(r.emsFormState).label,
      hint: formStatusOption(r.emsFormState).description
    },
    {
      label: "Client Submission",
      value: r.clientSubmissionState ? submissionStateOption(r.clientSubmissionState).label : "—",
      hint: r.clientSubmissionState ? submissionStateOption(r.clientSubmissionState).description : ""
    },
    { label: "Assigned Admin", value: text(r.assignedAdmin?.name) },
    { label: "CSE", value: text(r.cse?.name) },
    { label: "Requested By", value: `${r.requestedBy || "—"} · ${fmt.formatDateTime(r.createdOnUtc)}` }
  ];
});

const clientRows = computed(() => {
  const c = client.value;
  const rows = [
    { label: "Name", value: text(c.name) },
    { label: "Email", value: text(c.email) },
    { label: "Phone Number", value: text(c.mobileNumber) },
    { label: "Referral Source", value: text(c.referralSource) },
    { label: "Billing Address", value: addressText(c.billingAddress) }
  ];
  // The billing CONTACT is asked of individuals only now — every other entity type names one among its
  // contacts, listed above with a name, an email and a phone. Dropped when blank rather than shown as
  // "—", which beside a Contacts list that has a Billing Contact in it reads as a missing answer.
  if (c.billingContactName) rows.push({ label: "Billing Contact", value: text(c.billingContactName) });
  if (c.billingEmail) rows.push({ label: "Billing Email", value: text(c.billingEmail) });
  return rows;
});

const setupRows = computed(() => {
  const e = engagement.value;
  const rows = [
    { label: "Entity", value: `${e.entity?.name || "—"}${e.entity?.ein ? ` · EIN ${e.entity.ein}` : ""}` },
    { label: "Department", value: departmentLabel(e.department) },
    // Same industry-then-service sequence the setup form is filled in (the Entity Type itself is a
    // request field, so it sits in the request block above rather than here).
    { label: "Industry", value: subIndustryLabel(e.subIndustry) },
    { label: "Service Line", value: subServiceLineLabel(e.subServiceLine) },
    { label: "Department Director", value: text(e.departmentDirector?.name) },
    { label: "Engagement Executive", value: text(e.engagementExecutive?.name) },
    { label: "Billing Manager", value: text(e.billingManager?.name) }
  ];
  // Withheld figures get their own explanatory block in the template instead of a blank pair of fields.
  if (!e.financialsRestricted) {
    // One fee question per engagement — Assurance prices the engagement, GCS prices neither (its purchase
    // order and bill rate are on its own card), everyone else quotes a first year. The row the department
    // was never asked is absent rather than blank.
    if (showAssurance.value) {
      rows.push({ label: "Engagement Fee", value: money(e.engagementFee) });
    } else if (!showGcs.value) {
      rows.push({ label: "First-Year Fee Estimate", value: money(e.firstYearFeeEstimate) });
    }
    rows.push({ label: "% Realization", value: e.realizationPercentage == null ? "—" : `${e.realizationPercentage}%` });
  }
  return rows;
});

// Which questions this engagement was PUT, read off its department — and, for the contract block, the
// client's entity type beside it — exactly as the setup form decides what to ask. An approver reads the
// packet staff actually filled in, so the cards on this tab have to be the cards that were on that one.
//
// Keyed off the department rather than off "is there a detail row?", which is what these used to ask and
// which is wrong in both directions: an audit engagement whose CAF was never uploaded has no audit row at
// all, and an engagement moved from Tax to CAS keeps the tax row it was written with.
const department = computed(() => engagement.value.department);
const showAssurance = computed(() => isAssuranceDepartment(department.value));
const showGcs = computed(() => isGcsDepartment(department.value));
const showAttest = computed(() => requiresClientAcceptanceForm(department.value));
const showTax = computed(() => isTaxDepartment(department.value));
const showBilling = computed(() => isCasDepartment(department.value));
// The entity type is the REQUEST's, not the engagement's — same source the setup form reads it from.
const showGovernment = computed(() => isGovernmentAudit(department.value, request.value.industryGroup));

const taxForms = computed(() => engagement.value.tax?.taxForms || []);

const attestCardTitle = computed(() =>
  (showAssurance.value ? "Assurance — Client Acceptance Form & Fees" : "Audit — Client Acceptance Form"));

const assuranceRows = computed(() => {
  const a = engagement.value.audit || {};
  return [
    { label: "Fiscal Year End of Client", value: dateOnly(a.clientFiscalYearEnd) },
    {
      label: "Admin Fees",
      value: a.adminFeesApply
        ? (engagement.value.financialsRestricted ? "Yes" : (money(a.adminFeesAmount) || "Yes"))
        : "No"
    }
  ];
});

const gcsRows = computed(() => {
  const g = engagement.value.government || {};
  const restricted = engagement.value.financialsRestricted;
  return [
    { label: "Purchase Order No.", value: text(g.purchaseOrderNumber) },
    // The PO's value and the bill rate are money, and are withheld from a role that may not see the fee.
    { label: "Purchase Order Amount", value: restricted ? "Reserved" : money(g.purchaseOrderAmount) },
    { label: "PO Beginning Date", value: dateOnly(g.purchaseOrderStartDate) },
    { label: "PO Ending Date", value: dateOnly(g.purchaseOrderEndDate) },
    { label: "Personnel Level", value: text(personnelLevelLabel(g.personnelLevel)) },
    { label: "Bill Rate / Hour", value: restricted ? "Reserved" : money(g.billRatePerHour) }
  ];
});

// The uploaded purchase order as a stored-file row, so the approver can open it.
const purchaseOrderFile = computed(() => {
  const g = engagement.value.government;
  return g?.purchaseOrderMediaId
    ? { mediaId: g.purchaseOrderMediaId, fileName: g.purchaseOrderFileName || "Purchase Order" }
    : null;
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

// The Approval Status row is rendered as a badge in the template above rather than listed here, because
// it is the one value on this tab that is not a fact about the past.
const roundRows = computed(() => {
  const r = round.value;
  return [
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

// The signed CAF as a stored-file row. The packet resolves the media to a name and a URL, but the URL is
// not a link anyone can follow — /api/media is refused without a bearer token — so what the row needs
// from it is the media ID, which is what AppStoredFileItem fetches the bytes by.
const cafFile = computed(() => {
  const audit = engagement.value.audit;
  return audit?.clientAcceptanceFormMediaId
    ? { mediaId: audit.clientAcceptanceFormMediaId, fileName: audit.fileName || "Client Acceptance Form" }
    : null;
});

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
/* .rems-label / .rems-value / .rems-inner are the shared record vocabulary — see css/rems.scss. This
   screen used to carry its own copy of all three, which is how its field labels ended up teal while the
   same class rendered grey on the submitted-form panel beside it. */
.rems-card { border-radius: 12px; }
/* The approvers the round is still waiting on. A tint plus a left edge rather than a badge alone: the
   list is scanned down its left side, and the point is to find the waiting rows without reading each. */
.ar--awaiting {
  background: #fff8e1;
  box-shadow: inset 3px 0 0 #ff8f00;
}
/* The reader's own waiting row — the one they can actually act on — carries the accent in full. */
.ar--awaiting.ar--you {
  background: #fff3d6;
  box-shadow: inset 4px 0 0 var(--q-primary);
}
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
