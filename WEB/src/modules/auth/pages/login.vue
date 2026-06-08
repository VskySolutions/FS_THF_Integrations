<template>
  <q-card flat bordered class="auth-card q-pa-lg">
    <div class="q-mb-lg">
      <div class="text-h5 text-weight-bold">Welcome back 👋</div>
      <div class="text-body2 text-grey-7 q-mt-xs">Please sign in to your account to continue.</div>
    </div>

    <q-form greedy @submit.prevent.stop="login">
      <q-input
        v-model="model.username"
        outlined
        label="Username"
        stack-label
        hide-bottom-space
        maxlength="128"
        autofocus
        class="q-mb-md"
        :error="v$.username.$error"
        :error-message="v$.username.$errors[0]?.$message"
        @blur="v$.username.$touch"
      >
        <template #prepend>
          <q-icon name="o_person" />
        </template>
      </q-input>

      <q-input
        v-model="model.password"
        outlined
        label="Password"
        stack-label
        hide-bottom-space
        autocomplete="off"
        maxlength="28"
        :type="isPassword ? 'password' : 'text'"
        :error="v$.password.$error"
        :error-message="v$.password.$errors[0]?.$message"
        @blur="v$.password.$touch"
      >
        <template #prepend>
          <q-icon name="o_lock" />
        </template>
        <template #append>
          <q-icon :name="isPassword ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isPassword = !isPassword" />
        </template>
      </q-input>

      <div class="row items-center justify-between q-mt-sm q-mb-lg">
        <q-checkbox v-model="model.isRememberMeChecked" dense label="Remember me" color="primary" />
        <router-link :to="{ name: 'forgot_password', params: {} }" class="text-primary text-weight-medium auth-link">
          Forgot password?
        </router-link>
      </div>

      <q-btn label="Login" type="submit" color="primary" unelevated no-caps size="md" class="full-width" :loading="loading" />
    </q-form>
  </q-card>
</template>

<script setup>
import { ref } from "vue";
import useVuelidate from "@vuelidate/core";
import { required, helpers } from "@vuelidate/validators";
import { useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import { setLocalStorage, getLocalStorage, clearLocalStorage } from "assets/utils";
const router = useRouter();
const authStore = useAuthStore();

const loading = ref(false);
const isPassword = ref(true);

// Set Filters to local storage
const localStorageKey = "Login";
const filterLocalStorage = getLocalStorage(localStorageKey);

const username = filterLocalStorage ? filterLocalStorage.username : "";
const password = filterLocalStorage ? filterLocalStorage.password : "";
const isRememberMeChecked = filterLocalStorage ? filterLocalStorage.isRememberMeChecked : ref(false);

const model = ref({
  username,
  password,
  isRememberMeChecked
});

const rules = {
  username: { required: helpers.withMessage("Username is required", required) },
  password: { required: helpers.withMessage("Password is required", required) }
};

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });

const login = async () => {
  if (await v$.value.$validate()) {
    loading.value = true;
    authStore.login(model.value).then((resp) => {
      if (model.value.isRememberMeChecked === true) {
        setLocalStorage(localStorageKey, model.value);
      } else {
        clearLocalStorage(localStorageKey);
      }
      if (resp?.token) {
        localStorage.setItem("access_token", resp.token);
      }
      redirectToLanding();
    }).finally(() => {
      loading.value = false;
    });
  }
};

const redirectToLanding = () => {
  const landingPage = authStore.user?.siteLandingPageLink || "/";
  localStorage.setItem("last_route", landingPage);
  router.push(landingPage);
};
</script>

<style scoped>
.auth-card {
  width: 100%;
  border-radius: 16px;
}
.auth-link {
  text-decoration: none;
}
.auth-link:hover {
  text-decoration: underline;
}
</style>
