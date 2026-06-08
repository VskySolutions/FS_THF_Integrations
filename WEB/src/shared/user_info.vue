<template>
  <q-btn dense flat color="grey-8">
    <div class="flex items-center nav-user-info">
      <!-- <q-tooltip v-if="rolesNames.length > 0" anchor="bottom middle" self="top middle" class="text-capitalize">
        {{ rolesNames.join(", ") }}
      </q-tooltip> -->
      <q-icon v-if="model.virtualPath" class="material-icons-outlined q-mr-lg" size="38px">
        <img :src="model.virtualPath" alt="" style="width: 100px;">
      </q-icon>
      <q-icon v-else name="o_account_circle" color="orange" class="material-icons-outlined q-mr-sm" size="38px" />
      <div class="line-height-normal q-mr-sm text-left">
        <div class="fs-16 text-capitalize flex justify-between">
          <span>{{ user?.username && Array.isArray(user?.username) ? user?.username.join(" ") : user?.username }}</span>
          <q-icon side name="o_keyboard_arrow_down" size="sm" class="q-ml-sm" style="color:#697A8D;" />
        </div>
      </div>
    </div>
    <q-menu>
      <q-list style="min-width: 250px" class="user-card">
        <q-item class="q-py-sm">
          <q-item-section avatar>
            <q-icon v-if="model.virtualPath" class="material-icons-outlined q-mr-md" size="38px">
              <img :src="model.virtualPath" alt="" style="width: 100px;">
            </q-icon>
            <q-icon v-else name="o_account_circle" color="orange" class="material-icons-outlined q-mr-sm" size="38px" />
          </q-item-section>
          <q-item-section>
            <q-item-label class="text-h3 text-capitalize" lines="2">
              {{ user?.username && Array.isArray(user?.username) ? user?.username.join(" ") : user?.username }}
              <q-icon name="o_info" color="black" class="rounded-full q-ml-sm">
                <q-tooltip v-if="rolesNames.length > 0" class="text-capitalize">
                  {{ rolesNames.join(", ") }}
                </q-tooltip>
              </q-icon>
            </q-item-label>
          </q-item-section>
        </q-item>
        <q-separator class="q-mb-sm" />
        <q-item v-ripple :to="{ name: 'profile' }" clickable>
          <q-item-section avatar>
            <q-icon name="person" class="material-icons-outlined" color="orange" style="font-size: 25px;" />
            <q-icon name="image" class="material-icons-outlined" color="orange" style="font-size: 25px; margin-left: 15px; margin-top: -30px;" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Profile</q-item-label>
          </q-item-section>
        </q-item>
        <q-item v-ripple :to="{ name: 'change_password' }" clickable>
          <q-item-section avatar>
            <q-icon name="lock" color="orange" class="material-icons-outlined" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Change Password</q-item-label>
          </q-item-section>
        </q-item>
        <q-item v-ripple clickable @click="onLogout">
          <q-item-section avatar>
            <q-icon name="logout" color="orange" class="material-icons-outlined" />
          </q-item-section>
          <q-item-section>
            <q-item-label>Logout</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </q-menu>
  </q-btn>
</template>

<script setup>
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import _ from "lodash";

import accountService from "modules/account/account.service";
// ---------------------------------------------------------------------------------------------------
// Common variables
// ---------------------------------------------------------------------------------------------------

const router = useRouter();
const authStore = useAuthStore();
const user = authStore.user;
const rolesNames = user?.roles?.length > 0 ? user.roles : "";

const model = ref({
  virtualPath: ""
});

function getProfile () {
  accountService.getProfile().then(resp => {
    model.value = _.cloneDeep(resp);
    model.value.virtualPath = resp.picture ? resp.picture.virtualPath : "";
  });
}

const onLogout = () => {
  authStore.logout();
  router.replace({ name: "login" });
};

onMounted(() => {
  getProfile();
});

</script>
