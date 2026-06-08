<template>
  <q-card flat bordered class="auth-card q-pa-lg">
    <div class="q-mb-lg">
      <div class="text-h5 text-weight-bold">Set / Reset password</div>
      <div class="text-body2 text-grey-7 q-mt-xs">Choose a new password for your account.</div>
    </div>

    <q-form ref="zwform" greedy @submit.prevent.stop="submit">
      <q-input
        v-model="model.email"
        outlined
        label="Email address"
        stack-label
        hide-bottom-space
        readonly
        maxlength="128"
        class="q-mb-md"
      >
        <template #prepend>
          <q-icon name="o_mail" />
        </template>
      </q-input>

      <q-input
        v-model="model.newPassword"
        outlined
        label="New password"
        stack-label
        hide-bottom-space
        maxlength="20"
        class="q-mb-md"
        :type="isPassword ? 'password' : 'text'"
        :error="v$.newPassword.$error"
        :error-message="v$.newPassword.$errors[0]?.$message"
        @blur="v$.newPassword.$touch"
      >
        <template #prepend>
          <q-icon name="o_lock" />
        </template>
        <template #append>
          <q-icon :name="isPassword ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isPassword = !isPassword" />
        </template>
      </q-input>

      <q-input
        v-model="model.confirmPassword"
        outlined
        label="Confirm new password"
        stack-label
        hide-bottom-space
        maxlength="20"
        class="q-mb-md"
        :type="isPassword2 ? 'password' : 'text'"
        :error="v$.confirmPassword.$error"
        :error-message="v$.confirmPassword.$errors[0]?.$message"
        @blur="v$.confirmPassword.$touch"
      >
        <template #prepend>
          <q-icon name="o_lock" />
        </template>
        <template #append>
          <q-icon :name="isPassword2 ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isPassword2 = !isPassword2" />
        </template>
      </q-input>

      <q-banner dense class="bg-grey-2 text-grey-8 q-mb-lg auth-hint">
        <template #avatar>
          <q-icon name="o_info" color="primary" />
        </template>
        Use at least 8 characters with uppercase, lowercase, a number and a special character.
      </q-banner>

      <q-btn label="Submit" type="submit" color="primary" unelevated no-caps size="md" class="full-width" :loading="loading" />

      <div class="text-center q-mt-md">
        <q-btn flat no-caps color="primary" icon="o_arrow_back" label="Back to login" @click="$router.push('/auth/login')" />
      </div>
    </q-form>
  </q-card>
</template>

<script setup>
import { ref, onMounted } from "vue";
import useVuelidate from "@vuelidate/core";
import { required, helpers, email, minLength } from "@vuelidate/validators";
import authService from "modules/auth/auth.service";
import { notifyError, notifySuccess } from "assets/utils";
import { useRoute } from "vue-router";

const isPassword = ref(true);
const isPassword2 = ref(true);
const loading = ref(false);
const model = ref({
  email: "",
  newPassword: "",
  confirmPassword: ""
});

const rules = {
  email: {
    required: helpers.withMessage("Email is required", required),
    email: helpers.withMessage("Invalid email", email)
  },
  newPassword: { required: helpers.withMessage("New password is required", required), minLength: minLength(8), containsLowerCase: helpers.withMessage(() => "The password must contain a lowercase character", (value) => /[a-z]/.test(value)), containsUppercase: helpers.withMessage(() => "The password must contain an uppercase character", (value) => /[A-Z]/.test(value)), containsNumber: helpers.withMessage(() => "The password must contain a number", (value) => /[0-9]/.test(value)), containsSpecialCharacter: helpers.withMessage(() => "The password must contain special character", (value) => /[#?!@$%^&*-]/.test(value)) },
  confirmPassword: { required: helpers.withMessage("Confirm password is required", required) }
};

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });
const route = useRoute();
const userid = route.params.userid;

const submit = async () => {
  if (await v$.value.$validate()) {
    if (model.value.newPassword !== model.value.confirmPassword) {
      return notifyError({ message: "New password & Confirm Password are different." });
    }

    loading.value = true;
    authService.resetPassword(model.value).then((resp) => {
      notifySuccess({ message: "Your password has been reset successfully. Log in with your new password." });
    }).finally(() => {
      loading.value = false;
    });
  }
};

function getUser () {
  authService.getUser(userid).then((resp) => {
    model.value.email = resp.email;
  });
}

onMounted(() => {
  getUser();
});
</script>

<style scoped>
.auth-card {
  width: 100%;
  border-radius: 16px;
}
.auth-hint {
  border-radius: 8px;
}
</style>
