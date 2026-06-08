<template>
  <q-page padding>
    <div class="q-mx-auto" style="max-width: 960px;">
      <div class="text-h5 text-weight-bold q-mb-md">Account</div>

      <!-- Profile summary -->
      <q-card flat bordered>
        <q-card-section class="row items-center q-col-gutter-md">
          <q-avatar size="72px" color="primary" text-color="white">
            <span class="text-h5">{{ initials }}</span>
          </q-avatar>
          <div class="col">
            <div class="text-h6 text-capitalize">{{ fullName || user?.username || "—" }}</div>
            <div class="text-body2 text-grey-7">{{ user?.email || user?.userEmail || "No email on file" }}</div>
            <div class="q-mt-sm">
              <q-chip
                v-for="role in roles"
                :key="role"
                dense
                square
                color="primary"
                text-color="white"
                class="text-capitalize"
              >
                {{ role }}
              </q-chip>
            </div>
          </div>
          <q-btn
            unelevated
            color="primary"
            icon="o_edit"
            label="Edit Profile"
            no-caps
            :to="{ name: 'profile' }"
          />
        </q-card-section>
      </q-card>

      <!-- Details -->
      <div class="row q-col-gutter-md q-mt-sm">
        <div class="col-12 col-sm-6">
          <q-card flat bordered class="full-height">
            <q-card-section>
              <div class="text-subtitle2 text-grey-7 q-mb-sm">Site</div>
              <q-list dense>
                <q-item v-for="row in siteRows" :key="row.label" class="q-px-none">
                  <q-item-section>
                    <q-item-label caption>{{ row.label }}</q-item-label>
                    <q-item-label>{{ row.value || "—" }}</q-item-label>
                  </q-item-section>
                </q-item>
              </q-list>
            </q-card-section>
          </q-card>
        </div>

        <div class="col-12 col-sm-6">
          <q-card flat bordered class="full-height">
            <q-card-section>
              <div class="text-subtitle2 text-grey-7 q-mb-sm">Security</div>
              <div class="text-body2 text-grey-8 q-mb-md">
                Keep your account secure by updating your password regularly.
              </div>
              <q-btn
                outline
                color="primary"
                icon="o_lock"
                label="Change Password"
                no-caps
                :to="{ name: 'change_password' }"
              />
            </q-card-section>
          </q-card>
        </div>
      </div>
    </div>
  </q-page>
</template>

<script setup>
import { computed } from "vue";
import { useAuthStore } from "stores/auth";

const authStore = useAuthStore();
const user = authStore.user;

const fullName = computed(() =>
  [user?.firstName, user?.lastName].filter(Boolean).join(" ").trim());

const initials = computed(() => {
  const first = user?.firstName?.charAt(0) || user?.username?.charAt(0) || "";
  const last = user?.lastName?.charAt(0) || "";
  return (first + last).toUpperCase() || "U";
});

const roles = computed(() => (Array.isArray(user?.roles) ? user.roles : []));

const siteRows = computed(() => [
  { label: "Site Name", value: user?.siteName },
  { label: "Time Zone", value: user?.siteTimeZone },
  { label: "Username", value: user?.username }
]);
</script>

<style scoped>
.q-card {
  border-radius: 16px;
}
</style>
