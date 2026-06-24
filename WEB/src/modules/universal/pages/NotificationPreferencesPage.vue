<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'Home', icon: 'o_home', to: '/' },
        { label: 'Notifications', to: { name: 'uf_notifications' } },
        { label: 'Preferences' }
      ]"
      :back-to="{ name: 'uf_notifications' }"
    >
      <template #actions>
        <q-btn unelevated no-caps color="primary" icon="o_save" label="Save" :loading="saving" @click="save" />
      </template>
    </app-detail-header>

    <q-card flat bordered>
      <q-markup-table flat>
        <thead>
          <tr>
            <th class="text-left">Notification type</th>
            <th class="text-center">In-app</th>
            <th class="text-center">Email</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in prefs" :key="row.notificationType">
            <td class="text-left">
              <q-icon :name="metaFor(row.notificationType).icon" :color="metaFor(row.notificationType).color" class="q-mr-sm" />
              {{ metaFor(row.notificationType).label }}
            </td>
            <td class="text-center"><q-toggle v-model="row.inApp" /></td>
            <td class="text-center"><q-toggle v-model="row.email" /></td>
          </tr>
        </tbody>
      </q-markup-table>
    </q-card>
  </q-page>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { ufNotificationApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useNotificationMeta } from "composables/uf/useNotificationMeta";
import AppDetailHeader from "components/common/AppDetailHeader.vue";

const notify = useNotify();
const { metaFor } = useNotificationMeta();

const prefs = ref([]);
const saving = ref(false);

const load = async () => {
  try {
    prefs.value = (await ufNotificationApi.getPreferences()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const save = async () => {
  saving.value = true;
  try {
    await ufNotificationApi.updatePreferences(prefs.value.map((p) => ({
      notificationType: p.notificationType,
      inApp: p.inApp,
      email: p.email
    })));
    notify.success("Preferences saved.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

onMounted(load);
</script>
