<template>
  <q-page padding>
    <div class="q-mx-auto" style="max-width: 560px;">
      <div class="text-h5 text-weight-bold q-mb-md">Change Password</div>
      <q-card flat bordered class="account-card">
        <q-card-section class="row items-center q-gutter-sm">
          <q-icon name="o_lock" color="primary" size="sm" />
          <div class="text-subtitle1 text-weight-medium">Update your password</div>
        </q-card-section>
        <q-separator />
        <q-form greedy @submit.prevent.stop="onSubmit">
          <q-card-section>
            <q-input
              v-model="model.oldPassword" outlined stack-label hide-bottom-space label="Current Password *" maxlength="20"
              :type="isPassword ? 'password' : 'text'" autofocus class="q-mb-md"
              :error="v$.oldPassword.$error" :error-message="v$.oldPassword.$errors[0]?.$message" @blur="v$.oldPassword.$touch"
            >
              <template #prepend>
                <q-icon name="o_lock" />
              </template>
              <template #append>
                <q-icon :name="isPassword ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isPassword = !isPassword" />
              </template>
            </q-input>

            <q-input
              v-model="model.newPassword" outlined stack-label hide-bottom-space label="New Password *" maxlength="20"
              :type="isPassword2 ? 'password' : 'text'" class="q-mb-md"
              :error="v$.newPassword.$error" :error-message="v$.newPassword.$errors[0]?.$message" @blur="v$.newPassword.$touch"
            >
              <template #prepend>
                <q-icon name="o_lock_reset" />
              </template>
              <template #append>
                <q-icon :name="isPassword2 ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isPassword2 = !isPassword2" />
              </template>
            </q-input>

            <q-input
              v-model="model.confirmPassword" outlined stack-label hide-bottom-space label="Confirm Password *" maxlength="20"
              :type="isconfirmPassword ? 'password' : 'text'"
              :error="v$.confirmPassword.$error" :error-message="v$.confirmPassword.$errors[0]?.$message" @blur="v$.confirmPassword.$touch"
            >
              <template #prepend>
                <q-icon name="o_lock_reset" />
              </template>
              <template #append>
                <q-icon :name="isconfirmPassword ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="isconfirmPassword = !isconfirmPassword" />
              </template>
            </q-input>

            <q-banner dense class="bg-grey-2 text-grey-8 q-mt-md account-hint">
              <template #avatar>
                <q-icon name="o_info" color="primary" />
              </template>
              <div class="text-weight-medium q-mb-xs">Password requirements</div>
              <ul class="q-my-none q-pl-md">
                <li>Minimum 8 characters long — the more, the better</li>
                <li>At least one lowercase character</li>
                <li>At least one uppercase character</li>
                <li>At least one number and one special character</li>
              </ul>
            </q-banner>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right" class="q-pa-md">
            <q-btn flat color="grey-8" label="Cancel" type="button" no-caps :to="{ name: 'account' }" />
            <q-btn unelevated color="primary" label="Set New Password" type="submit" no-caps :loading="loading" />
          </q-card-actions>
        </q-form>
      </q-card>
    </div>
  </q-page>
</template>

<script setup>
import { ref } from "vue";
import useVuelidate from "@vuelidate/core";
import { required, helpers, minLength } from "@vuelidate/validators";
import accountService from "modules/account/account.service";
import { notifySuccess } from "assets/utils";
import { useRouter } from "vue-router";

const router = useRouter();

const isPassword = ref(true);
const isPassword2 = ref(true);
const isconfirmPassword = ref(true);
const loading = ref(false);

const model = ref({
  oldPassword: "",
  newPassword: "",
  confirmPassword: ""
});

const rules = {
  oldPassword: { required: helpers.withMessage("Current password is required", required) },
  newPassword: { required: helpers.withMessage("New password is required", required), minLength: minLength(8), containsLowerCase: helpers.withMessage(() => "The password must contain a lowercase character", (value) => /[a-z]/.test(value)), containsUppercase: helpers.withMessage(() => "The password must contain an uppercase character", (value) => /[A-Z]/.test(value)), containsNumber: helpers.withMessage(() => "The password must contain a number", (value) => /[0-9]/.test(value)), containsSpecialCharacter: helpers.withMessage(() => "The password must contain special character", (value) => /[#?!@$%^&*-]/.test(value)) },
  confirmPassword: { required: helpers.withMessage("Confirm password is required", required) }
};

const v$ = useVuelidate(rules, model, { $lazy: true, $autoDirty: true });

const onSubmit = async () => {
  if (await v$.value.$validate()) {
    loading.value = true;
    accountService.changePassword(model.value).then((resp) => {
      notifySuccess({ message: "The password has been changed successfully." });
      router.push({ name: "login", params: {} });
    }).finally(() => {
      loading.value = false;
    });
  }
};
</script>

<style scoped>
.account-card {
  border-radius: 16px;
}
.account-hint {
  border-radius: 8px;
}
.account-hint ul li {
  margin: 2px 0;
}
</style>
