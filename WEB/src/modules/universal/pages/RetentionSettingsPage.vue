<template>
  <q-page padding>
    <div class="text-h6 q-mb-md">Deleted Records Retention</div>

    <q-card flat bordered class="q-pa-md" style="max-width: 520px;">
      <p class="text-grey-7">
        Soft-deleted records are kept for this many days before they are flagged as overdue for permanent deletion.
      </p>
      <div class="row items-center q-gutter-sm">
        <q-input v-model.number="retentionDays" type="number" outlined dense label="Retention (days)" style="max-width: 180px;" />
        <q-btn unelevated no-caps color="primary" icon="o_save" label="Save" :loading="saving" @click="save" />
      </div>
    </q-card>

    <q-card flat bordered class="q-pa-md q-mt-md" style="max-width: 520px;">
      <div class="text-subtitle1 text-weight-medium q-mb-sm">Overdue records</div>
      <div v-if="!overdue.length" class="text-grey-6">No records are past their retention period.</div>
      <q-list v-else dense>
        <q-item v-for="o in overdue" :key="o.entityType">
          <q-item-section>{{ labelFor(o.entityType) }}</q-item-section>
          <q-item-section side>
            <q-badge :color="o.count ? 'orange-8' : 'grey-5'" :label="`${o.count} overdue`" />
          </q-item-section>
        </q-item>
      </q-list>
    </q-card>
  </q-page>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { ufDeletedApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useEntityMeta } from "composables/uf/useEntityMeta";

const notify = useNotify();
const { labelFor } = useEntityMeta();

const retentionDays = ref(90);
const saving = ref(false);
const overdue = ref([]);

const load = async () => {
  try {
    const config = await ufDeletedApi.getRetention();
    retentionDays.value = config?.retentionDays ?? 90;
    overdue.value = (await ufDeletedApi.overdue()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const save = async () => {
  saving.value = true;
  try {
    const config = await ufDeletedApi.updateRetention(retentionDays.value);
    retentionDays.value = config?.retentionDays ?? retentionDays.value;
    notify.success("Retention period saved.");
    overdue.value = (await ufDeletedApi.overdue()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

onMounted(load);
</script>
