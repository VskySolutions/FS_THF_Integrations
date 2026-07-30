<template>
  <div>
    <!-- Entity identity header. -->
    <div class="row items-center q-mb-md">
      <div class="col">
        <div class="text-h6">{{ entity.name }}</div>
        <div class="text-caption text-grey-6">
          <span v-if="entity.ein">EIN {{ entity.ein }} · </span>
          {{ entity.isMainEntity ? "Main entity" : "Related entity" }}
        </div>
      </div>
      <q-badge v-if="engagement" :color="statusMeta.color" class="q-pa-sm text-body2">{{ statusMeta.label }}</q-badge>
    </div>

    <q-banner v-if="!engagement" dense class="bg-grey-2 text-grey-8 rounded-borders">
      <template #avatar><q-icon name="o_info" color="grey-7" /></template>
      This entity does not have an engagement yet.
    </q-banner>

    <template v-else>
      <q-banner v-if="!editable" dense class="bg-blue-1 text-blue-9 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_lock" color="blue-9" /></template>
        This engagement is {{ statusMeta.label.toLowerCase() }} and is read-only.
      </q-banner>

      <q-tabs
        v-model="innerTab" dense align="left" active-color="primary" indicator-color="primary"
        class="text-grey-7"
      >
        <q-tab name="setup" icon="o_engineering" label="Setup" no-caps />
        <q-tab name="marketing" icon="o_campaign" label="Marketing" no-caps />
        <q-tab name="commission" icon="o_payments" label="Commission" no-caps />
        <q-tab name="approval" icon="o_approval" label="Approval" no-caps :disable="!marketingSaved">
          <q-tooltip v-if="!marketingSaved">Save at least one marketing method to unlock approval</q-tooltip>
        </q-tab>
      </q-tabs>
      <q-separator />

      <q-tab-panels v-model="innerTab" keep-alive animated>
        <q-tab-panel name="setup" class="q-px-none">
          <!-- Read-only entity addresses + contacts (from the submitted graph; kept in sync after Copy From). -->
          <q-expansion-item
            icon="o_home_work" label="Addresses & contacts" dense-toggle
            class="rems-details q-mb-md"
          >
            <div class="q-pa-sm">
              <div class="rems-detail-grid">
                <div>
                  <div class="rems-detail__label">Physical</div>
                  <div class="rems-detail__value">{{ addressText("Physical") }}</div>
                </div>
                <div>
                  <div class="rems-detail__label">Mailing</div>
                  <div class="rems-detail__value">{{ addressText("Mailing") }}</div>
                </div>
              </div>
              <div class="rems-detail__label q-mt-sm">Contacts</div>
              <div v-if="entity.contacts && entity.contacts.length" class="column q-gutter-xs">
                <div v-for="c in entity.contacts" :key="c.id" class="rems-detail__value">
                  <span class="text-weight-medium">{{ roleText(c.role) }}:</span> {{ c.name || "—" }}
                  <span class="text-grey-6">({{ c.email || "no email" }} · {{ c.phone || "no phone" }})</span>
                </div>
              </div>
              <div v-else class="rems-detail__value text-grey-6">No contacts.</div>
            </div>
          </q-expansion-item>

          <engagement-setup-form
            :engagement="engagement"
            :staff="staff"
            :dept-options="deptOptions"
            :service-line-options="serviceLineOptions"
            :tax-form-options="taxFormOptions"
            :tax-form-unavailable="taxFormUnavailable"
            :other-entities="otherEntities"
            :editable="editable"
            @saved="onSaved"
            @workspace-refresh="$emit('workspace-refresh')"
          />
        </q-tab-panel>

        <q-tab-panel name="marketing" class="q-px-none">
          <engagement-marketing
            :engagement="engagement"
            :marketing-groups="marketingGroups"
            :marketing-unavailable="marketingUnavailable"
            :editable="editable"
            @saved="onSaved"
          />
        </q-tab-panel>

        <q-tab-panel name="commission" class="q-px-none">
          <engagement-commission
            :engagement="engagement"
            :staff="staff"
            :editable="editable"
            @saved="onSaved"
          />
        </q-tab-panel>

        <q-tab-panel name="approval" class="q-px-none">
          <engagement-approval
            :engagement="engagement"
            :can-send="canSendApproval"
            :marketing-saved="marketingSaved"
            @status-changed="onStatusChanged"
          />
        </q-tab-panel>
      </q-tab-panels>
    </template>
  </div>
</template>

<script setup>
// One entity's engagement workspace: an inner tab set (Setup / Marketing / Commission / Approval) over the
// entity's single engagement. The panel owns the engagement as the source of truth — each child save
// returns the refreshed engagement view, which the panel adopts and pushes back down.
import { ref, computed, watch } from "vue";
import EngagementSetupForm from "modules/rems/components/engagement/EngagementSetupForm.vue";
import EngagementMarketing from "modules/rems/components/engagement/EngagementMarketing.vue";
import EngagementCommission from "modules/rems/components/engagement/EngagementCommission.vue";
import EngagementApproval from "modules/rems/components/engagement/EngagementApproval.vue";

const props = defineProps({
  entity: { type: Object, required: true },
  staff: { type: Array, default: () => [] },
  deptOptions: { type: Array, default: () => [] },
  serviceLineOptions: { type: Array, default: () => [] },
  marketingGroups: { type: Array, default: () => [] },
  marketingUnavailable: { type: Boolean, default: false },
  taxFormOptions: { type: Array, default: () => [] },
  taxFormUnavailable: { type: Boolean, default: false },
  otherEntities: { type: Array, default: () => [] },
  canSendApproval: { type: Boolean, default: false }
});
defineEmits(["workspace-refresh"]);

const STATUS_META = {
  Draft: { label: "Draft", color: "grey-6" },
  PendingApproval: { label: "Pending Approval", color: "orange-8" },
  Rejected: { label: "Rejected", color: "negative" },
  Approved: { label: "Approved", color: "positive" }
};

const innerTab = ref("setup");

// The engagement is the panel's source of truth; re-seed it whenever the parent supplies a fresh entity.
const engagement = ref(props.entity.engagement);
watch(() => props.entity, (e) => { engagement.value = e.engagement; });

// Editable only while Draft or Rejected (matches the backend lock); marketing-saved unlocks approval.
const editable = computed(() => ["Draft", "Rejected"].includes(engagement.value?.status));
const marketingSaved = computed(() => (engagement.value?.marketingMethodIds?.length || 0) > 0);
const statusMeta = computed(() => STATUS_META[engagement.value?.status] || { label: engagement.value?.status, color: "grey-6" });

const onSaved = (view) => { engagement.value = view; };
const onStatusChanged = (status) => { engagement.value = { ...engagement.value, status }; };

// ---- Read-only entity detail helpers ----
const addressText = (type) => {
  const row = (props.entity.addresses || []).find((a) => a.addressType === type);
  const a = row?.address;
  if (!a) return "—";
  const line2 = [a.city, a.state, a.zip].filter((x) => x && String(x).trim()).join(" ");
  const parts = [a.street, line2].filter((x) => x && String(x).trim());
  return parts.length ? parts.join(", ") : "—";
};
const roleText = (r) => (r || "").replace(/([a-z])([A-Z])/g, "$1 $2");
</script>

<style scoped>
.rems-details { border: 1px solid #e0e6ed; border-radius: 10px; }
.rems-detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 24px;
}
.rems-detail__label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: #7a8699;
  margin-bottom: 2px;
}
.rems-detail__value {
  font-size: 14px;
  color: #2c3540;
  word-break: break-word;
}
</style>
